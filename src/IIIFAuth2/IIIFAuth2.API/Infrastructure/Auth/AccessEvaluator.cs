using System.Net;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Settings;
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
/// Decides whether the current request can access a deliverable with specified roles. Used by both probe and
/// verify-access, which differ only in how the session is found (access-token vs cookie)
/// </summary>
public class AccessEvaluator(
    IpRoleEvaluator ipRoleEvaluator,
    AppointmentRoleEvaluator appointmentRoleEvaluator,
    IOptions<ApiSettings> apiOptions)
{
    private readonly ApiSettings apiSettings = apiOptions.Value;

    /// <summary>
    /// Evaluate access. IP roles are evaluated first as they need no session, then the session's roles, then
    /// appointments held by the session.
    /// </summary>
    /// <param name="deliverableId">Deliverable being accessed</param>
    /// <param name="roles">Roles, any of which grants access</param>
    /// <param name="asset">Asset metadata, used to evaluate appointment asset patterns. Null if not provided</param>
    /// <param name="getSession">Delegate to find the current session (e.g. from access-token or cookie)</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    public async Task<AccessDecision> Evaluate(DeliverableId deliverableId, IReadOnlyCollection<string> roles,
        AssetMetadata? asset, Func<CancellationToken, Task<TryGetSessionResponse>> getSession,
        CancellationToken cancellationToken = default)
    {
        var customerId = deliverableId.Customer;

        var ipRoleEvaluation = await ipRoleEvaluator.Evaluate(customerId, roles);
        if (ipRoleEvaluation.HasGrantedRole) return AccessDecision.Granted;
        if (ipRoleEvaluation.AreAllIpRoles(roles)) return GetLocationDecision(ipRoleEvaluation);

        var sessionResponse = await getSession(cancellationToken);
        if (sessionResponse.CanUserAccessAtLeastOneRole(roles)) return AccessDecision.Granted;

        var session = sessionResponse.IsSuccessWithSession() ? sessionResponse.SessionUser : null;
        var appointmentEvaluation = await appointmentRoleEvaluator.Evaluate(customerId, deliverableId.Space, roles,
            session, asset, cancellationToken);
        if (appointmentEvaluation.HasGrantedRole) return AccessDecision.Granted;

        if (session == null) return GetNoSessionDecision(sessionResponse.Status, appointmentEvaluation);

        return appointmentEvaluation.HasAppointmentRoles
            ? GetAppointmentDecision(appointmentEvaluation)
            : new AccessDecision(HttpStatusCode.Forbidden, "Forbidden", "Session does not have required roles");
    }

    private AccessDecision GetLocationDecision(IpRoleEvaluation ipRoleEvaluation)
    {
        var note = apiSettings.UseGenericLocationWording || ipRoleEvaluation.LocationPhrases.Count == 0
            ? "Only available from permitted locations"
            : $"Only available {string.Join(" or ", ipRoleEvaluation.LocationPhrases)}";
        return new AccessDecision(HttpStatusCode.Forbidden, "Not available at your location", note);
    }

    private static AccessDecision GetNoSessionDecision(GetSessionStatus status,
        AppointmentRoleEvaluation appointmentEvaluation)
    {
        var (heading, note) = status switch
        {
            GetSessionStatus.MissingSession => ("Invalid credentials", "Authorising credentials invalid"),
            GetSessionStatus.MissingCredentials => ("Missing credentials", "Authorising credentials not found"),
            GetSessionStatus.ExpiredSession => ("Expired session", "Session has expired"),
            _ => ("Unknown Error", "Unable to fulfil request")
        };
        if (appointmentEvaluation.HasAppointmentRoles) note += ", or enter your appointment key";
        return new AccessDecision(HttpStatusCode.Unauthorized, heading, note);
    }

    private static AccessDecision GetAppointmentDecision(AppointmentRoleEvaluation evaluation)
        => evaluation.Reason switch
        {
            AppointmentDenialReason.NotStarted => new AccessDecision(HttpStatusCode.Forbidden,
                "Your appointment has not started", $"Your appointment starts at {evaluation.Start:u}"),
            AppointmentDenialReason.NotActive => new AccessDecision(HttpStatusCode.Forbidden,
                "Appointment not yet approved", "Your appointment is awaiting approval"),
            AppointmentDenialReason.Ended => new AccessDecision(HttpStatusCode.Forbidden,
                "Your appointment has ended", $"Your appointment ended at {evaluation.End:u}"),
            AppointmentDenialReason.AssetNotMatched => new AccessDecision(HttpStatusCode.Forbidden,
                "Not part of your appointment", "This item is not included in your appointment"),
            _ => new AccessDecision(HttpStatusCode.Unauthorized, "Appointment required",
                "Enter your appointment key"),
        };
}
