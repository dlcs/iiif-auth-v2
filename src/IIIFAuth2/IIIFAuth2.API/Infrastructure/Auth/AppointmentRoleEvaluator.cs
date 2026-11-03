using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// Evaluates requested roles that are granted by Appointment role-providers against the appointments held by a
/// session
/// </summary>
public class AppointmentRoleEvaluator(
    AuthServicesContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AppointmentRoleEvaluator> logger)
{
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

        var appointments = await dbContext.SessionAppointments
            .AsNoTracking()
            .Where(sa => sa.SessionUserId == session.Id)
            .Select(sa => sa.Appointment)
            .ToListAsync(cancellationToken);
        var relevant = appointments.Where(a => a.Roles.Any(appointmentRoles.Contains)).ToList();

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

        if (granted.Count > 0) return new AppointmentRoleEvaluation(appointmentRoles, granted);
        if (results.Count == 0)
            return new AppointmentRoleEvaluation(appointmentRoles, granted, AppointmentDenialReason.NoAppointment);

        var reason = results.Min(r => r.Reason!.Value);
        var withReason = results.Where(r => r.Reason == reason).Select(r => r.Appointment).ToList();
        return new AppointmentRoleEvaluation(appointmentRoles, granted, reason,
            reason == AppointmentDenialReason.NotStarted ? withReason.Min(a => a.Start) : null,
            reason == AppointmentDenialReason.Ended ? withReason.Max(a => a.End) : null);
    }

    private static AppointmentDenialReason? GetDenialReason(Appointment appointment, DateTime now, int customerId,
        int space, AssetMetadata? asset)
    {
        if (appointment.Start > now) return AppointmentDenialReason.NotStarted;
        if (!appointment.Active) return AppointmentDenialReason.NotActive;
        if (appointment.End <= now) return AppointmentDenialReason.Ended;
        if (!appointment.AssetPatterns.Matches(customerId, space, asset)) return AppointmentDenialReason.AssetNotMatched;
        return null;
    }
}
