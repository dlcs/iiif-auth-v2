using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth;
using IIIFAuth2.API.Infrastructure.Auth.Models;
using MediatR;

namespace IIIFAuth2.API.Features.Access.Requests;

/// <summary>
/// Handle request for access token. Will return <see cref="AuthAccessToken2"/> or
/// <see cref="AuthAccessTokenError2"/>
/// </summary>
public class HandleAccessTokenRequest : IRequest<JsonLdBase?>
{
    public int CustomerId { get; }
    public string Origin { get; }
    public string MessageId { get; }

    public HandleAccessTokenRequest(int customerId, Uri origin, string messageId)
    {
        CustomerId = customerId;
        Origin = origin.ToString();
        MessageId = messageId;
    }
}

public class HandleAccessTokenRequestHandler : IRequestHandler<HandleAccessTokenRequest, JsonLdBase?>
{
    private readonly SessionManagementService sessionManagementService;

    public HandleAccessTokenRequestHandler(
        SessionManagementService sessionManagementService)
    {
        this.sessionManagementService = sessionManagementService;
    }
    
    public async Task<JsonLdBase?> Handle(HandleAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var findSessionResponse =
            await sessionManagementService.TryGetSessionUserForCookie(request.CustomerId, request.Origin,
                cancellationToken);
        
        return BuildResponse(findSessionResponse, request.MessageId);
    }

    private static JsonLdBase BuildResponse(TryGetSessionResponse tryGetSessionResponse, string messageId)
        => tryGetSessionResponse.Status == GetSessionStatus.Success && tryGetSessionResponse.SessionUser != null
            ? BuildAccessTokenResponse(tryGetSessionResponse.SessionUser, messageId)
            : BuildAccessTokenError(tryGetSessionResponse.Status, messageId);

    private static AuthAccessToken2 BuildAccessTokenResponse(SessionUser sessionUser, string messageId)
        => new()
        {
            MessageId = messageId,
            AccessToken = sessionUser.AccessToken,
            ExpiresIn = (int)(sessionUser.Expires - DateTime.UtcNow).TotalSeconds,
        };

    private static AuthAccessTokenError2 BuildAccessTokenError(GetSessionStatus getSessionStatus, string messageId)
    {
        var (profile, heading, note) = GetErrorProps(getSessionStatus);
        return AuthServiceBuilder.CreateAuthAccessTokenError2(profile, messageId, heading, note);
    }

    private static (string Profile, string Heading, string Note) GetErrorProps(GetSessionStatus status)
        => status switch
        {
            GetSessionStatus.MissingSession => (AuthAccessTokenError2.InvalidAspect, "Invalid cookie",
                "Authorising cookie invalid"),
            GetSessionStatus.DifferentOrigin => (AuthAccessTokenError2.InvalidOrigin, "Origin invalid",
                "Requested origin differs from access service request"),
            GetSessionStatus.MissingCookie => (AuthAccessTokenError2.MissingAspect, "Missing cookie",
                "Authorising cookie not found"),
            GetSessionStatus.InvalidCookie => (AuthAccessTokenError2.InvalidAspect, "Invalid cookie",
                "Authorising cookie invalid"),
            GetSessionStatus.ExpiredSession => (AuthAccessTokenError2.ExpiredAspect, "Expired session",
                "Authorising cookie found but expired"),
            GetSessionStatus.UnknownError => (AuthAccessTokenError2.Unavailable, "Request cannot be fulfilled",
                "Unexpected error fulfilling request"),
            GetSessionStatus.Success => (AuthAccessTokenError2.Unavailable, "Request cannot be fulfilled",
                "Unexpected error fulfilling request"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
}