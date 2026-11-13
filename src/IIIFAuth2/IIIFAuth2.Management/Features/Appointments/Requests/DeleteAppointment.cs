using IIIFAuth2.Core.Data;
using IIIFAuth2.Management.Infrastructure.Requests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.Appointments.Requests;

/// <summary>
/// Delete appointment. Sessions holding it are unlinked by FK cascade
/// </summary>
public record DeleteAppointment(int CustomerId, string Token) : IRequest<DeleteResult>;

public class DeleteAppointmentHandler(AuthServicesContext dbContext) : IRequestHandler<DeleteAppointment, DeleteResult>
{
    public async Task<DeleteResult> Handle(DeleteAppointment request, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.Appointments
            .GetAppointment(request.CustomerId, request.Token)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0 ? DeleteResult.Deleted : DeleteResult.NotFound;
    }
}
