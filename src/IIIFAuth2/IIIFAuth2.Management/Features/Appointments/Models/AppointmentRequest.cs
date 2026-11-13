using System.ComponentModel.DataAnnotations;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.Management.Features.Appointments.Models;

/// <summary>
/// Request body for POST and PUT. Required fields are enforced by model validation, omitted optional fields are
/// cleared
/// </summary>
/// <remarks>Value types are nullable so that an omitted value fails [Required] rather than binding as default</remarks>
public class AppointmentRequest : IAppointmentFields
{
    [Required] public string? Name { get; set; }
    [Required, EmailAddress] public string? Email { get; set; }
    public string? Notes { get; set; }
    [Required] public DateTime? Start { get; set; }
    [Required] public DateTime? End { get; set; }
    [Required] public bool? Active { get; set; }
    public string? ApprovedBy { get; set; }
    [Required] public List<string>? Roles { get; set; }
    [Required] public AssetPatterns? AssetPatterns { get; set; }
}