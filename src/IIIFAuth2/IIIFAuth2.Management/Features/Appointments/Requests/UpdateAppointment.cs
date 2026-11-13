using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Management.Features.Appointments.Models;
using IIIFAuth2.Management.Infrastructure.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.Appointments.Requests;

/// <summary>
/// Update an existing appointment. PUT (<see cref="IsPatch"/> false) replaces all mutable fields, PATCH only
/// updates supplied fields
/// </summary>
/// <remarks>
/// API caches appointments per session for Auth:RefreshThreshold so changes can take that long to take effect
/// </remarks>
public record UpdateAppointment(int CustomerId, string Token, IAppointmentFields Request, bool IsPatch)
    : IRequest<ModifyEntityResult<Appointment>>;

public class UpdateAppointmentHandler(AuthServicesContext dbContext, AppointmentRules appointmentRules)
    : IRequestHandler<UpdateAppointment, ModifyEntityResult<Appointment>>
{
    public async Task<ModifyEntityResult<Appointment>> Handle(UpdateAppointment request,
        CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .GetAppointment(request.CustomerId, request.Token)
            .SingleOrDefaultAsync(cancellationToken);
        if (appointment == null) return new ModifyEntityResult<Appointment>(WriteResult.NotFound);

        var error = await appointmentRules.ApplyAndValidate(appointment, request.Request, !request.IsPatch,
            cancellationToken);
        if (error != null) return ModifyEntityResult<Appointment>.Failure(error, WriteResult.FailedValidation);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ModifyEntityResult<Appointment>.Success(appointment);
    }
}
