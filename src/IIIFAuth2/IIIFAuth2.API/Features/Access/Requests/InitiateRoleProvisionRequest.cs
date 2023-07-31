using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using IIIFAuth2.API.Utils;
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
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<InitiateRoleProvisionRequestHandler> logger;

    public InitiateRoleProvisionRequestHandler(
        RoleProviderService roleProviderService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InitiateRoleProvisionRequestHandler> logger)
    {
        this.roleProviderService = roleProviderService;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }
    
    public async Task<HandleRoleProvisionResponse?> Handle(InitiateRoleProvisionRequest request, CancellationToken cancellationToken)
    {
        var originMatchesHost = OriginMatchesHost(request);
        var handled = await roleProviderService.HandleRequest(request.CustomerId, request.AccessServiceName,
            originMatchesHost, request.Origin, cancellationToken);
        return handled;
    }

    private bool OriginMatchesHost(InitiateRoleProvisionRequest request)
    {
        var httpContext = httpContextAccessor.HttpContext.ThrowIfNull(nameof(httpContextAccessor.HttpContext));
        var originMatchesHost = httpContext.Request.IsSameOrigin(request.Origin);
        logger.LogTrace("Test Origin {RequestOrigin} with Host {Scheme}://{Host} result: {OriginMatchesHost}", request.Origin,
            httpContext.Request.Scheme, httpContext.Request.Host, originMatchesHost);
        return originMatchesHost;
    }
}