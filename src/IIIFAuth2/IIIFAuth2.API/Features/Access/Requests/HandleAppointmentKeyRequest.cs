using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Handle an appointment key posted from the appointment key form. A valid key adds the appointment to the user's
/// session (creating one if required) and issues a cookie
/// </summary>
public record HandleAppointmentKeyRequest(int CustomerId, string AccessServiceName, string? AppointmentKey, Uri Origin)
    : IRequest<HandleRoleProvisionResponse?>;

public class HandleAppointmentKeyRequestHandler
    : IRequestHandler<HandleAppointmentKeyRequest, HandleRoleProvisionResponse?>
{
    private readonly RoleProviderService roleProviderService;

    public HandleAppointmentKeyRequestHandler(RoleProviderService roleProviderService)
    {
        this.roleProviderService = roleProviderService;
    }

    public Task<HandleRoleProvisionResponse?> Handle(HandleAppointmentKeyRequest request,
        CancellationToken cancellationToken)
        => roleProviderService.HandleAppointmentKey(request.CustomerId, request.AccessServiceName,
            request.AppointmentKey, request.Origin, cancellationToken);
}
