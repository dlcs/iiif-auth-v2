using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Management.Infrastructure.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.Appointments.Requests;

/// <summary>
/// Get single Appointment
/// </summary>
public record GetAppointment(int CustomerId, string Token) : IRequest<FetchEntityResult<Appointment>>;

public class GetAppointmentHandler(AuthServicesContext dbContext)
    : IRequestHandler<GetAppointment, FetchEntityResult<Appointment>>
{
    public async Task<FetchEntityResult<Appointment>> Handle(GetAppointment request,
        CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.AsNoTracking()
            .GetAppointment(request.CustomerId, request.Token)
            .SingleOrDefaultAsync(cancellationToken);
        return appointment == null
            ? FetchEntityResult<Appointment>.NotFound()
            : FetchEntityResult<Appointment>.Success(appointment);
    }
}
