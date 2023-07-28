using IIIFAuth2.API.Infrastructure.Auth;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Handle posted back token from significant gesture - this will initialise a user-session if token valid
/// or return an error message if invalid
/// </summary>
public class HandleRoleProvisionToken : IRequest<string?>
{
    public string SingleUseToken { get; }

    public HandleRoleProvisionToken(string singleUseToken)
    {
        SingleUseToken = singleUseToken;
    }
}

public class HandleSignificantGestureHandler : IRequestHandler<HandleRoleProvisionToken, string?>
{
    private readonly SessionManagementService sessionManagementService;
    private const string InvalidTokenError = "Token invalid or expired";

    public HandleSignificantGestureHandler(SessionManagementService sessionManagementService)
    {
        this.sessionManagementService = sessionManagementService;
    }
    
    public async Task<string?> Handle(HandleRoleProvisionToken request, CancellationToken cancellationToken)
    {
        var sessionUserResult =
            await sessionManagementService.TryCreateSessionFromToken(request.SingleUseToken, cancellationToken);

        return sessionUserResult.Success ? null : InvalidTokenError;
    }
}