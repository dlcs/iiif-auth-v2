namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// The result of evaluating requested roles against the appointments held by a session
/// </summary>
/// <param name="AppointmentRoles">Requested roles that are granted by an Appointment role-provider</param>
/// <param name="GrantedRoles">Requested roles granted by an appointment held by the session, for this asset</param>
/// <param name="Denials">Why each appointment role that wasn't granted was denied, keyed by role</param>
public record AppointmentRoleEvaluation(
    IReadOnlySet<string> AppointmentRoles,
    IReadOnlySet<string> GrantedRoles,
    IReadOnlyDictionary<string, AppointmentDenial> Denials)
{
    public static readonly AppointmentRoleEvaluation Empty =
        new(new HashSet<string>(), new HashSet<string>(), new Dictionary<string, AppointmentDenial>());
}

/// <summary>
/// Why an appointment role was not granted
/// </summary>
/// <param name="Start">Start of the appointment, if <see cref="Reason"/> is NotStarted</param>
/// <param name="End">End of the appointment, if <see cref="Reason"/> is Ended</param>
public record AppointmentDenial(AppointmentDenialReason Reason, DateTime? Start = null, DateTime? End = null)
{
    public static readonly AppointmentDenial NoAppointment = new(AppointmentDenialReason.NoAppointment);
}

/// <summary>
/// Why appointment roles were not granted. Ordered by priority, lowest value is reported if several apply
/// </summary>
public enum AppointmentDenialReason
{
    /// <summary>Session holds no appointment granting the role</summary>
    NoAppointment,
    NotStarted,
    NotActive,
    Ended,
    AssetNotMatched,
}
