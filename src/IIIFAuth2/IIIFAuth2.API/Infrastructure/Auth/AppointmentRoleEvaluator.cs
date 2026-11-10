using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Settings;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Z.EntityFramework.Plus;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Evaluates requested roles that are granted by Appointment role-providers against the appointments held by a
/// session
/// </summary>
public class AppointmentRoleEvaluator(
    AuthServicesContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuthSettings> authOptions,
    ILogger<AppointmentRoleEvaluator> logger)
{
    private readonly AuthSettings authSettings = authOptions.Value;

    /// <summary>
    /// Determine which of the specified roles are appointment roles, and which of those are granted by the
    /// appointments held by <paramref name="session"/> for the asset
    /// </summary>
    /// <remarks>
    /// A role is an appointment role if its access-service's role-provider is an Appointment provider. The
    /// "appointment:" prefix of the role is not used to determine this
    /// </remarks>
    public async Task<AppointmentRoleEvaluation> Evaluate(int customerId, int space, IReadOnlyCollection<string> roles,
        SessionUser? session, AssetMetadata? asset, CancellationToken cancellationToken = default)
    {
        var appointmentRoles = (await RoleProviderLookup.GetRoleConfigurations<AppointmentConfiguration>(
                dbContext, httpContextAccessor, logger, customerId, roles, RoleX.IsAppointmentRole))
            .Keys.ToHashSet();
        if (appointmentRoles.Count == 0) return AppointmentRoleEvaluation.Empty;

        if (session == null)
            return new AppointmentRoleEvaluation(appointmentRoles, new HashSet<string>(),
                AppointmentDenialReason.NoAppointment);

        var relevant = await GetSessionAppointments(session.Id, appointmentRoles, cancellationToken);

        var now = DateTime.UtcNow;
        var results = relevant.Select(a => (Appointment: a, Reason: GetDenialReason(a, now, customerId, space, asset)))
            .ToList();
        var granted = results
            .Where(r => r.Reason == null)
            .SelectMany(r => r.Appointment.Roles)
            .Where(appointmentRoles.Contains)
            .ToHashSet();

        logger.LogDebug(
            "Session {SessionId} granted {GrantedCount} of {AppointmentRoleCount} appointment roles from {AppointmentCount} appointments",
            session.Id, granted.Count, appointmentRoles.Count, relevant.Count);

        return granted.Count > 0
            ? new AppointmentRoleEvaluation(appointmentRoles, granted)
            : BuildDenial(appointmentRoles, results);
    }

    /// <summary>
    /// Build denial from the highest priority reason, with start/end of the relevant appointments if applicable
    /// </summary>
    private static AppointmentRoleEvaluation BuildDenial(IReadOnlySet<string> appointmentRoles,
        List<(Appointment Appointment, AppointmentDenialReason? Reason)> results)
    {
        if (results.Count == 0)
            return new AppointmentRoleEvaluation(appointmentRoles, new HashSet<string>(),
                AppointmentDenialReason.NoAppointment);

        var reason = results.Min(r => r.Reason!.Value);
        var withReason = results.Where(r => r.Reason == reason).Select(r => r.Appointment).ToList();
        return new AppointmentRoleEvaluation(appointmentRoles, new HashSet<string>(), reason,
            reason == AppointmentDenialReason.NotStarted ? withReason.Min(a => a.Start) : null,
            reason == AppointmentDenialReason.Ended ? withReason.Max(a => a.End) : null);
    }

    /// <summary>
    /// Get appointments held by session that grant any of <paramref name="appointmentRoles"/>. Called for every
    /// request for an appointment-restricted asset (e.g. each image tile) so cached. All of the session's
    /// appointments are cached and filtered in memory, as roles aren't queryable (stored comma-joined) and filtering
    /// in the query would cache per role set. Time window and patterns are evaluated per request
    /// </summary>
    /// <remarks>
    /// Expired by tag when an appointment is added to the session on this instance. Other instances see the change
    /// when their entry expires
    /// </remarks>
    private async Task<List<Appointment>> GetSessionAppointments(Guid sessionUserId,
        IReadOnlySet<string> appointmentRoles, CancellationToken cancellationToken)
    {
        var appointments = await dbContext.SessionAppointments
            .AsNoTracking()
            .Where(sa => sa.SessionUserId == sessionUserId)
            .Select(sa => sa.Appointment)
            .FromCacheAsync(
                new MemoryCacheEntryOptions
                    { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(authSettings.RefreshThreshold) },
                cancellationToken, CacheKeys.SessionAppointments(sessionUserId));
        return appointments.Where(a => a.Roles.Any(appointmentRoles.Contains)).ToList();
    }

    private static AppointmentDenialReason? GetDenialReason(Appointment appointment, DateTime now, int customerId,
        int space, AssetMetadata? asset)
    {
        // Patterns first: an appointment that never covers the asset mustn't report when it starts/ends
        if (!appointment.AssetPatterns.Matches(customerId, space, asset)) return AppointmentDenialReason.AssetNotMatched;
        if (appointment.Start > now) return AppointmentDenialReason.NotStarted;
        if (!appointment.Active) return AppointmentDenialReason.NotActive;
        if (appointment.End <= now) return AppointmentDenialReason.Ended;
        return null;
    }
}
