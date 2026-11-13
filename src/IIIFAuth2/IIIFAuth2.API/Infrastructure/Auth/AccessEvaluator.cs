using System.Net;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Infrastructure.Auth;

/// <summary>
/// The result of evaluating access to a deliverable. Heading and Note are user-facing wording, null if access granted
/// </summary>
public record AccessDecision(HttpStatusCode StatusCode, string? Heading = null, string? Note = null)
{
    public static readonly AccessDecision Granted = new(HttpStatusCode.OK);
}

/// <summary>
/// Decides whether the current request can access a deliverable with specified role entries. Used by both probe and
/// verify-access, which differ only in how the session is found (access-token vs cookie)
/// </summary>
/// <remarks>
/// Every role in every entry is Met, Achievable (a user action could meet it, e.g. sign in or enter an appointment
/// key) or Blocked. An entry is Met if all roles are met, Blocked if any role is blocked, else Achievable. Access is
/// 200 if any entry Met, 401 if any Achievable, else 403
/// </remarks>
public class AccessEvaluator(
    IpRoleEvaluator ipRoleEvaluator,
    AppointmentRoleEvaluator appointmentRoleEvaluator,
    IOptions<ApiSettings> apiOptions)
{
    private readonly ApiSettings apiSettings = apiOptions.Value;

    /// <summary>
    /// Evaluate access. IP roles are evaluated first as they need no session, then the session's roles and the
    /// appointments held by the session.
    /// </summary>
    /// <param name="deliverableId">Deliverable being accessed</param>
    /// <param name="entries">Role entries, any one of which grants access if all of its roles are met</param>
    /// <param name="asset">Asset metadata, used to evaluate appointment asset patterns. Null if not provided</param>
    /// <param name="getSession">Delegate to find the current session (e.g. from access-token or cookie)</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    public async Task<AccessDecision> Evaluate(DeliverableId deliverableId, IReadOnlyList<RoleEntry> entries,
        AssetMetadata? asset, Func<CancellationToken, Task<TryGetSessionResponse>> getSession,
        CancellationToken cancellationToken = default)
    {
        var customerId = deliverableId.Customer;
        var roles = entries.AllRoles();

        // IP roles need no session, so avoid the session lookup if IP alone decides
        var ip = await ipRoleEvaluator.Evaluate(customerId, roles);
        if (entries.Any(e => e.Roles.All(ip.GrantedRoles.Contains))) return AccessDecision.Granted;
        if (entries.All(e => e.Roles.Any(r => ip.IpRoles.Contains(r) && !ip.GrantedRoles.Contains(r))))
            return GetLocationDecision(ip);

        var sessionResponse = await getSession(cancellationToken);
        var session = sessionResponse.IsSuccessWithSession() ? sessionResponse.SessionUser : null;
        var appointments = await appointmentRoleEvaluator.Evaluate(customerId, deliverableId.Space, roles,
            session, asset, cancellationToken);

        var results = entries
            .Select(e => e.Roles.Select(r => GetRoleResult(r, ip, appointments, session)).ToList())
            .ToList();
        if (results.Any(e => e.All(r => r.State == RoleState.Met))) return AccessDecision.Granted;

        var achievable = results.Where(e => e.All(r => r.State != RoleState.Blocked)).ToList();
        if (achievable.Count > 0)
        {
            return session == null
                ? GetNoSessionDecision(sessionResponse.Status,
                    achievable.Any(e => e.Any(r => appointments.AppointmentRoles.Contains(r.Role))))
                // With a session, only an appointment role can still be achievable. Revisit if sessions accumulate
                // roles (#10), as a missing clickthrough/oidc role would then also be achievable
                : new AccessDecision(HttpStatusCode.Unauthorized, "Appointment required",
                    "Enter your appointment key");
        }

        return GetBlockedDecision(results, ip);
    }

    private static RoleResult GetRoleResult(string role, IpRoleEvaluation ip,
        AppointmentRoleEvaluation appointments, SessionUser? session)
    {
        if (ip.IpRoles.Contains(role))
            return ip.GrantedRoles.Contains(role)
                ? RoleResult.Met(role)
                : new RoleResult(role, RoleState.Blocked, BlockReason.Location);

        if (appointments.AppointmentRoles.Contains(role))
        {
            if (appointments.GrantedRoles.Contains(role)) return RoleResult.Met(role);
            var denial = appointments.Denials[role];
            return denial.Reason == AppointmentDenialReason.NoAppointment
                ? new RoleResult(role, RoleState.Achievable)
                : new RoleResult(role, RoleState.Blocked, BlockReason.Appointment, denial);
        }

        if (session == null) return new RoleResult(role, RoleState.Achievable);
        return session.Roles.Contains(role)
            ? RoleResult.Met(role)
            : new RoleResult(role, RoleState.Blocked, BlockReason.Forbidden);
    }

    /// <summary>
    /// Wording for a 403 comes from the entry closest to being met (fewest unmet roles, then most met roles), and
    /// the highest priority reason it is blocked
    /// </summary>
    private AccessDecision GetBlockedDecision(List<List<RoleResult>> results, IpRoleEvaluation ip)
    {
        var blocked = results
            .Select(e => (Unmet: e.Count(r => r.State != RoleState.Met), Met: e.Count(r => r.State == RoleState.Met),
                Reason: e.Where(r => r.State == RoleState.Blocked).MinBy(r => r.Block)!))
            .OrderBy(e => e.Unmet)
            .ThenByDescending(e => e.Met)
            .ThenBy(e => e.Reason.Block)
            .First()
            .Reason;

        return blocked.Block switch
        {
            BlockReason.Appointment => GetAppointmentDecision(blocked.Denial!),
            BlockReason.Location => GetLocationDecision(ip),
            _ => new AccessDecision(HttpStatusCode.Forbidden, "Forbidden", "Session does not have required roles"),
        };
    }

    private AccessDecision GetLocationDecision(IpRoleEvaluation ipRoleEvaluation)
    {
        var note = apiSettings.UseGenericLocationWording || ipRoleEvaluation.LocationPhrases.Count == 0
            ? "Only available from permitted locations"
            : $"Only available {string.Join(" or ", ipRoleEvaluation.LocationPhrases)}";
        return new AccessDecision(HttpStatusCode.Forbidden, "Not available at your location", note);
    }

    private static AccessDecision GetNoSessionDecision(GetSessionStatus status, bool appointmentAchievable)
    {
        var (heading, note) = status switch
        {
            GetSessionStatus.MissingSession => ("Invalid credentials", "Authorising credentials invalid"),
            GetSessionStatus.MissingCredentials => ("Missing credentials", "Authorising credentials not found"),
            GetSessionStatus.ExpiredSession => ("Expired session", "Session has expired"),
            _ => ("Unknown Error", "Unable to fulfil request")
        };
        if (appointmentAchievable) note += ", or enter your appointment key";
        return new AccessDecision(HttpStatusCode.Unauthorized, heading, note);
    }

    private static AccessDecision GetAppointmentDecision(AppointmentDenial denial)
        => denial.Reason switch
        {
            AppointmentDenialReason.NotStarted => new AccessDecision(HttpStatusCode.Forbidden,
                "Your appointment has not started", $"Your appointment starts at {denial.Start:u}"),
            AppointmentDenialReason.NotActive => new AccessDecision(HttpStatusCode.Forbidden,
                "Appointment not yet approved", "Your appointment is awaiting approval"),
            AppointmentDenialReason.Ended => new AccessDecision(HttpStatusCode.Forbidden,
                "Your appointment has ended", $"Your appointment ended at {denial.End:u}"),
            AppointmentDenialReason.AssetNotMatched => new AccessDecision(HttpStatusCode.Forbidden,
                "Not part of your appointment", "This item is not included in your appointment"),
            _ => new AccessDecision(HttpStatusCode.Unauthorized, "Appointment required",
                "Enter your appointment key"),
        };

    private enum RoleState { Met, Achievable, Blocked }

    /// <summary>Why a role is blocked, ordered by priority when choosing 403 wording</summary>
    private enum BlockReason { Appointment, Forbidden, Location }

    private record RoleResult(string Role, RoleState State, BlockReason? Block = null,
        AppointmentDenial? Denial = null)
    {
        public static RoleResult Met(string role) => new(role, RoleState.Met);
    }
}
