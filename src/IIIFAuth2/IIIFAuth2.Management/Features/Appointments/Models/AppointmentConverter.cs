using IIIFAuth2.Core.Data.Entities;

namespace IIIFAuth2.Management.Features.Appointments.Models;

public static class AppointmentConverter
{
    /// <summary>
    /// Converts specific Appointment entity to <see cref="HydraAppointment"/>
    /// </summary>
    public static HydraAppointment ToHydra(this Appointment appointment, string baseUrl)
        => new()
        {
            Id = $"{baseUrl}/customers/{appointment.Customer}/appointments/{appointment.Token}",
            Token = appointment.Token,
            Name = appointment.Name,
            Email = appointment.Email,
            Notes = appointment.Notes,
            Start = appointment.Start,
            End = appointment.End,
            Active = appointment.Active,
            ApprovedBy = appointment.ApprovedBy,
            DateApproved = appointment.DateApproved,
            Created = appointment.Created,
            Roles = appointment.Roles,
            AssetPatterns = appointment.AssetPatterns,
        };
}
