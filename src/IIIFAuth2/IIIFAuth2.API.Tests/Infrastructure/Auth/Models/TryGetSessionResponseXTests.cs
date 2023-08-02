using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Auth.Models;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth.Models;

public class TryGetSessionResponseXTests
{
    [Theory]
    [InlineData(GetSessionStatus.MissingSession)]
    [InlineData(GetSessionStatus.DifferentOrigin)]
    [InlineData(GetSessionStatus.MissingCredentials)]
    [InlineData(GetSessionStatus.InvalidCookie)]
    [InlineData(GetSessionStatus.ExpiredSession)]
    [InlineData(GetSessionStatus.UnknownError)]
    public void IsSuccessful_False_IfNotSuccess_AndHasSessionUser(GetSessionStatus status)
    {
        // Arrange
        var tryGetSession = new TryGetSessionResponse(status, new SessionUser());
        
        // Assert
        tryGetSession.IsSuccessWithSession().Should().BeFalse();
    }
    
    [Fact]
    public void IsSuccessful_False_IfSuccessStatus_NoSessionUser()
    {
        // Arrange
        var tryGetSession = new TryGetSessionResponse(GetSessionStatus.Success);
        
        // Assert
        tryGetSession.IsSuccessWithSession().Should().BeFalse();
    }

    [Fact]
    public void IsSuccessful_True_IfSuccessStatus_AndHasSessionUser()
    {
        // Arrange
        var tryGetSession = new TryGetSessionResponse(GetSessionStatus.Success, new SessionUser());
        
        // Assert
        tryGetSession.IsSuccessWithSession().Should().BeTrue();
    }

    [Theory]
    [InlineData(GetSessionStatus.MissingSession)]
    [InlineData(GetSessionStatus.DifferentOrigin)]
    [InlineData(GetSessionStatus.MissingCredentials)]
    [InlineData(GetSessionStatus.InvalidCookie)]
    [InlineData(GetSessionStatus.ExpiredSession)]
    [InlineData(GetSessionStatus.UnknownError)]
    public void CanUserAccessAtLeastOneRole_False_IfNotSuccess(GetSessionStatus status)
    {
        // Arrange
        var roles = new List<string> { "foo" };
        var tryGetSession = new TryGetSessionResponse(status, new SessionUser { Roles = roles });
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeFalse();
    }
    
    [Fact]
    public void CanUserAccessAtLeastOneRole_False_IfNoSessionUser()
    {
        // Arrange
        var roles = new List<string> { "foo" };
        var tryGetSession = new TryGetSessionResponse(GetSessionStatus.Success);
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeFalse();
    }
    
    [Fact]
    public void CanUserAccessAtLeastOneRole_False_NullRoles()
    {
        // Arrange
        var roles = new List<string> { "foo" };
        var tryGetSession = new TryGetSessionResponse(GetSessionStatus.Success, new SessionUser());
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeFalse();
    }
    
    [Fact]
    public void CanUserAccessAtLeastOneRole_False_EmptyRoles()
    {
        // Arrange
        var roles = new List<string> { "foo" };
        var tryGetSession =
            new TryGetSessionResponse(GetSessionStatus.Success, new SessionUser { Roles = new List<string>(0) });
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeFalse();
    }
    
    [Fact]
    public void CanUserAccessAtLeastOneRole_False_NoMatches()
    {
        // Arrange
        var roles = new List<string> { "foo" };
        var tryGetSession =
            new TryGetSessionResponse(GetSessionStatus.Success, new SessionUser { Roles = new List<string> { "bar" } });
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeFalse();
    }
    
    [Fact]
    public void CanUserAccessAtLeastOneRole_True_IfMatch()
    {
        // Arrange
        var roles = new List<string> { "foo", "bar" };
        var tryGetSession =
            new TryGetSessionResponse(GetSessionStatus.Success,
                new SessionUser { Roles = new List<string> { "baz", "bar" } });
        
        // Assert
        tryGetSession.CanUserAccessAtLeastOneRole(roles).Should().BeTrue();
    }
}