using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.Management.Features.Appointments.Models;

/// <summary>
/// Request body for PATCH, only non-null fields are updated
/// </summary>
public class AppointmentPatch : IAppointmentFields
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public bool? Active { get; set; }
    public string? ApprovedBy { get; set; }
    public List<string>? Roles { get; set; }
    public AssetPatterns? AssetPatterns { get; set; }
}