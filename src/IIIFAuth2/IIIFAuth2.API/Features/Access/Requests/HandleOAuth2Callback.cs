using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Handle the oauth2 callback request, exchanging auth code for id_token
/// </summary>
public class HandleOAuth2Callback : IRequest<HandleRoleProvisionResponse?>
{
    public int CustomerId { get; }
    public string AccessServiceName { get; }
    public string Code { get; }
    public string RoleProvisionToken { get; }

    public HandleOAuth2Callback(int customerId, string accessServiceName, string code, string roleProvisionToken)
    {
        CustomerId = customerId;
        AccessServiceName = accessServiceName;
        Code = code;
        RoleProvisionToken = roleProvisionToken;
    }
}

public class HandleOAuth2CallbackHandler : IRequestHandler<HandleOAuth2Callback, HandleRoleProvisionResponse?>
{
    private readonly RoleProviderService roleProviderService;

    public HandleOAuth2CallbackHandler(RoleProviderService roleProviderService)
    {
        this.roleProviderService = roleProviderService;
    }

    public async Task<HandleRoleProvisionResponse?> Handle(HandleOAuth2Callback request,
        CancellationToken cancellationToken)
    {
        var handled = await roleProviderService.HandleOidcCallback(request.CustomerId, request.AccessServiceName,
            request.RoleProvisionToken, request.Code, cancellationToken);
        return handled;
    }
}