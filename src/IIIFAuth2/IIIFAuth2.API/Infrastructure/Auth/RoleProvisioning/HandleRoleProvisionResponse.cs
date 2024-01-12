using IIIFAuth2.API.Infrastructure.Auth.Models;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

public class HandleRoleProvisionResponse
{
    /// <summary>
    /// If true the user needs to carry out a gesture on dlcs domain prior to issuing cookie
    /// </summary>
    public bool SignificantGestureRequired { get; private init; }

    /// <summary>
    /// If true the RoleProvider has finished an no further action is required
    /// </summary>
    public bool RoleProvisionHandled { get; private init; }
    
    /// <summary>
    /// If true the RoleProvider request needs to be redirected to continue processing (e.g. for login to oidc provider)
    /// </summary>
    public bool RequiresRedirect { get; private init; }
    
    public Uri? RedirectUri { get; private init; }

    public SignificantGestureModel? SignificantGestureModel { get; private init; }

    public static HandleRoleProvisionResponse Handled() => new() { RoleProvisionHandled = true };

    public static HandleRoleProvisionResponse SignificantGesture(SignificantGestureModel model) => new()
        { SignificantGestureRequired = true, SignificantGestureModel = model };

    public static HandleRoleProvisionResponse Redirect(Uri redirectUri) =>
        new() { RedirectUri = redirectUri, RequiresRedirect = true };

    public static readonly HandleRoleProvisionResponse Empty = new();
}