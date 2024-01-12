using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Initiate a role-provision request, this can either initiate calls to determine roles, create user session and issue
/// a cookie or request a significant gesture be complete prior to issuing token
/// </summary>
public class InitiateRoleProvisionRequest : IRequest<HandleRoleProvisionResponse?>
{
    public int CustomerId { get; }
    public string AccessServiceName { get; }
    public Uri Origin { get; }
    
    public InitiateRoleProvisionRequest(int customerId, string accessServiceName, Uri origin)
    {
        CustomerId = customerId;
        AccessServiceName = accessServiceName;
        Origin = origin;
    }
}

public class InitiateRoleProvisionRequestHandler : IRequestHandler<InitiateRoleProvisionRequest, HandleRoleProvisionResponse?>
{
    private readonly RoleProviderService roleProviderService;

    public InitiateRoleProvisionRequestHandler(RoleProviderService roleProviderService)
    {
        this.roleProviderService = roleProviderService;
    }
    
    public async Task<HandleRoleProvisionResponse?> Handle(InitiateRoleProvisionRequest request, CancellationToken cancellationToken)
    {
        var handled = await roleProviderService.HandleInitialRequest(request.CustomerId, request.AccessServiceName,
            request.Origin, cancellationToken);
        return handled;
    }
}