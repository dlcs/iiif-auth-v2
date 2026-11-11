namespace IIIFAuth2.Management.Features.Appointments.Models;

/// <summary>
/// Query parameters for searching appointments. All are optional and combined with AND
/// </summary>
public class AppointmentSearch
{
    /// <summary>Case-insensitive substring match on token</summary>
    public string? Token { get; set; }

    /// <summary>Case-insensitive substring match on name</summary>
    public string? Name { get; set; }

    /// <summary>Case-insensitive substring match on email</summary>
    public string? Email { get; set; }

    public bool? Active { get; set; }
    public DateTime? StartFrom { get; set; }
    public DateTime? StartTo { get; set; }
    public DateTime? EndFrom { get; set; }
    public DateTime? EndTo { get; set; }

    /// <summary>Shorthand for active=false and end >= now</summary>
    public bool AwaitingApproval { get; set; }
}