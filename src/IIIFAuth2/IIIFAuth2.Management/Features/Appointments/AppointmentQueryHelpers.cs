using IIIFAuth2.Core.Data.Entities;

namespace IIIFAuth2.Management.Features.Appointments;

public static class AppointmentQueryHelpers
{
    extension(IQueryable<Appointment> query)
    {
        /// <summary>
        /// Get all appointments for customer
        /// </summary>
        public IQueryable<Appointment> GetCustomerAppointments(int customer)
            => query.Where(a => a.Customer == customer);

        /// <summary>
        /// Get specified appointment for customer
        /// </summary>
        public IQueryable<Appointment> GetAppointment(int customer, string token)
            => query
                .GetCustomerAppointments(customer)
                .Where(a => a.Token == token);
    }
}