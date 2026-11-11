using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Infrastructure.Hydra;

namespace IIIFAuth2.Management.Features.Appointments.Models;

/// <summary>
/// Hydra representation of an Appointment
/// </summary>
public class HydraAppointment() : JsonLdBase("Appointment")
{
    public string Token { get; set; } = null!;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Active { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? DateApproved { get; set; }
    public DateTime Created { get; set; }
    public List<string> Roles { get; set; } = [];
    public AssetPatterns AssetPatterns { get; set; } = new();
}