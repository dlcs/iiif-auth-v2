namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// The result of evaluating requested roles against the appointments held by a session
/// </summary>
/// <param name="AppointmentRoles">Requested roles that are granted by an Appointment role-provider</param>
/// <param name="GrantedRoles">Requested roles granted by an appointment held by the session, for this asset</param>
/// <param name="Reason">Why no role was granted, null if any were granted or there are no appointment roles</param>
/// <param name="Start">Start of the appointment, if <see cref="Reason"/> is NotStarted</param>
/// <param name="End">End of the appointment, if <see cref="Reason"/> is Ended</param>
public record AppointmentRoleEvaluation(
    IReadOnlySet<string> AppointmentRoles,
    IReadOnlySet<string> GrantedRoles,
    AppointmentDenialReason? Reason = null,
    DateTime? Start = null,
    DateTime? End = null)
{
    public static readonly AppointmentRoleEvaluation Empty = new(new HashSet<string>(), new HashSet<string>());

    public bool HasAppointmentRoles => AppointmentRoles.Count > 0;

    public bool HasGrantedRole => GrantedRoles.Count > 0;
}

/// <summary>
/// Why appointment roles were not granted. Ordered by priority, lowest value is reported if several apply
/// </summary>
public enum AppointmentDenialReason
{
    /// <summary>Session holds no appointment granting any requested role</summary>
    NoAppointment,
    NotStarted,
    NotActive,
    Ended,
    AssetNotMatched,
}
