using IIIFAuth2.API.Infrastructure.Auth.Models;

namespace IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning;

public class HandleRoleProvisionResponse
{
    public bool SignificantGestureRequired { get; private init; }

    public bool RoleProvisionHandled { get; private init; }

    public SignificantGestureModel? SignificantGestureModel { get; private init; }

    public static HandleRoleProvisionResponse Handled() => new() { RoleProvisionHandled = true };

    public static HandleRoleProvisionResponse SignificantGesture(SignificantGestureModel model) => new()
        { SignificantGestureRequired = true, SignificantGestureModel = model };
}