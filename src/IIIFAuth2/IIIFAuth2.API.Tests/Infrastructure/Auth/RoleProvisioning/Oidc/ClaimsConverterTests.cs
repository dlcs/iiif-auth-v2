using System.Security.Claims;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Models.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace IIIFAuth2.API.Tests.Infrastructure.Auth.RoleProvisioning.Oidc;

public class ClaimsConverterTests
{
    private readonly ClaimsConverter sut = new(new NullLogger<ClaimsConverter>());

    [Fact]
    public void GetDlcsRolesFromClaims_Unsuccessful_IfClaimNotFound()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/notfound", "foo"));
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Value.Should().BeNullOrEmpty();
    }
    
    [Fact]
    public void GetDlcsRolesFromClaims_ReturnsMappedValue_IfMappingFound()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "foo"));
        var mappedRoles = new[]{"http://dlcs.roles/my-test-role", "http://dlcs.roles/other-role" };
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            Mapping = new Dictionary<string, string[]> { { "foo", mappedRoles } }
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(mappedRoles);
    }
    
    [Fact]
    public void GetDlcsRolesFromClaims_ClaimValue_IfClaimFoundButValueUnknown_AndUnknownValueBehaviourIsUseClaim()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var expectedRoles = new[] { "bar" };
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.UseClaim,
            Mapping = new Dictionary<string, string[]>
            {
                { "foo", new[] { "http://dlcs.roles/my-test-role", "http://dlcs.roles/other-role" } }
            }
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedRoles);
    }
    
    [Fact]
    public void GetDlcsRolesFromClaims_ClaimValue_IfClaimFound_NoMappings_AndUnknownValueBehaviourIsUseClaim()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var expectedRoles = new[] { "bar" };
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.UseClaim,
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedRoles);
    }
    
    [Fact]
    public void GetDlcsRolesFromClaims_FallbackValue_IfClaimFoundButValueUnknown_AndUnknownValueBehaviourIsFallback()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var expectedRoles = new[] { "http://dlcs.roles/the-fallback-role" };
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.Fallback,
            FallbackMapping = new[] { "http://dlcs.roles/the-fallback-role" },
            Mapping = new Dictionary<string, string[]>
            {
                { "foo", new[] { "http://dlcs.roles/my-test-role", "http://dlcs.roles/other-role" } }
            }
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedRoles);
    }
    
    [Fact]
    public void GetDlcsRolesFromClaims_FallbackValue_IfClaimFound_NoMappings_AndUnknownValueBehaviourIsFallback()
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var expectedRoles = new[] { "http://dlcs.roles/the-fallback-role" };
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.Fallback,
            FallbackMapping = new[] { "http://dlcs.roles/the-fallback-role" },
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedRoles);
    }
    
    [Theory]
    [InlineData(UnknownMappingValueBehaviour.Unknown)]
    [InlineData(UnknownMappingValueBehaviour.Throw)]
    public void GetDlcsRolesFromClaims_Throws_IfClaimFoundButValueUnknown_AndUnknownValueBehaviourIsUnknownOrThrow(UnknownMappingValueBehaviour behaviour)
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = behaviour,
            FallbackMapping = new[] { "http://dlcs.roles/the-fallback-role" },
            Mapping = new Dictionary<string, string[]>
            {
                { "foo", new[] { "http://dlcs.roles/my-test-role", "http://dlcs.roles/other-role" } }
            }
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Value.Should().BeNullOrEmpty();
    }
    
    [Theory]
    [InlineData(UnknownMappingValueBehaviour.Unknown)]
    [InlineData(UnknownMappingValueBehaviour.Throw)]
    public void GetDlcsRolesFromClaims_Throws_IfClaimFound_NoMappings_AndUnknownValueBehaviourIsUnknownOrThrow(UnknownMappingValueBehaviour behaviour)
    {
        // Arrange
        var claimsPrincipal = GetPrincipal(new Claim("http://test.example/found", "bar"));
        var oidcConfig = new OidcConfiguration
        {
            ClaimType = "http://test.example/found",
            UnknownValueBehaviour = behaviour,
            FallbackMapping = new[] { "http://dlcs.roles/the-fallback-role" },
        };
        
        // Act
        var result = sut.GetDlcsRolesFromClaims(claimsPrincipal, oidcConfig);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Value.Should().BeNullOrEmpty();
    }

    private ClaimsPrincipal GetPrincipal(params Claim[] claims) => new(new ClaimsIdentity(claims));
}