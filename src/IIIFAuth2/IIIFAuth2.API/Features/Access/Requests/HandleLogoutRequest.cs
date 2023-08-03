using IIIFAuth2.API.Infrastructure.Auth;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Log specified user out of session for customer
/// </summary>
public class HandleLogoutRequest : IRequest<bool>
{
    public int CustomerId { get; }
    public string AccessServiceName { get; }

    public HandleLogoutRequest(int customerId, string accessServiceName)
    {
        CustomerId = customerId;
        AccessServiceName = accessServiceName;
    }
}

public class HandleLogoutRequestHandler : IRequestHandler<HandleLogoutRequest, bool>
{
    private readonly SessionCleaner sessionCleaner;

    public HandleLogoutRequestHandler(SessionCleaner sessionCleaner)
    {
        this.sessionCleaner = sessionCleaner;
    }
    
    public async Task<bool> Handle(HandleLogoutRequest request, CancellationToken cancellationToken)
    {
        var success = await sessionCleaner.LogoutUser(request.CustomerId, cancellationToken);
        return success;
    }
}