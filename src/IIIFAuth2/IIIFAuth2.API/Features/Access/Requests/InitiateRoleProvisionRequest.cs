using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using IIIFAuth2.API.Utils;
using MediatR;

namespace IIIFAuth2.API.Features.Auth.Requests;

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
    private readonly IHttpContextAccessor httpContextAccessor;
    
    public InitiateRoleProvisionRequestHandler(
        RoleProviderService roleProviderService,
        IHttpContextAccessor httpContextAccessor)
    {
        this.roleProviderService = roleProviderService;
        this.httpContextAccessor = httpContextAccessor;
    }
    
    public async Task<HandleRoleProvisionResponse?> Handle(InitiateRoleProvisionRequest request, CancellationToken cancellationToken)
    {
        var originMatchesHost = OriginMatchesHost(request);
        var handled = await roleProviderService.HandleRequest(request.CustomerId, request.AccessServiceName,
            originMatchesHost, cancellationToken);
        return handled;
    }

    private bool OriginMatchesHost(InitiateRoleProvisionRequest request)
        => httpContextAccessor.HttpContext?.Request.IsSameOrigin(request.Origin) ?? false;
}