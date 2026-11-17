using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.Management.Features.Appointments.Models;

/// <summary>
/// Appointment fields accepted on write. Token and created are server-managed so not accepted.
/// Customer is automatically set.
/// </summary>
public interface IAppointmentFields
{
    string? Name { get; }
    string? Email { get; }
    string? Notes { get; }
    DateTime? Start { get; }
    DateTime? End { get; }
    bool? Active { get; }
    string? ApprovedBy { get; }
    List<string>? Roles { get; }
    AssetPatterns? AssetPatterns { get; }
}