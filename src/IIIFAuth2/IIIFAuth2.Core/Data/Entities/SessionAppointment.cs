namespace IIIFAuth2.Core.Data.Entities;

/// <summary>
/// Links a <see cref="SessionUser"/> to an <see cref="Appointment"/> whose key they have entered
/// </summary>
public class SessionAppointment
{
    public Guid SessionUserId { get; set; }
    public SessionUser SessionUser { get; set; } = null!;
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
