using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Models.Converters;

namespace IIIFAuth2.API.Tests.Models.Converters;

public class AccessServiceConverterTests
{
    [Fact]
    public void ToIIIFModel_ConvertsMinimalAccessService()
    {
        // Arrange
        var accessService = new AccessService
        {
            Id = Guid.Empty,
            Profile = "active",
            Customer = 99,
            Name = "test access service",
        };

        var expected = new AuthAccessService2
        {
            Id = "todo",
            Profile = "active",
            Service = new List<IService>
            {
                new AuthAccessTokenService2
                {
                    Id = "todo",
                },
                new AuthLogoutService2
                {
                    Id = "todo",
                    Label = new LanguageMap("en", "Logout of test access service")
                }
            }
        };

        // Act
        var actual = accessService.ToIIIFModel();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void ToIIIFModel_ConvertsFullAccessService()
    {
        // Arrange
        var accessService = new AccessService
        {
            Id = Guid.Empty,
            Profile = "active",
            Customer = 99,
            Name = "test access service",
            ConfirmLabel = new LanguageMap("en", "confirm"),
            Label = new LanguageMap("en", "label"),
            Heading = new LanguageMap("en", "heading"),
            Note = new LanguageMap("en", "note"),
            LogoutLabel = new LanguageMap("en", "logout-label"),
            AccessTokenErrorHeading = new LanguageMap("en", "error heading"),
            AccessTokenErrorNote = new LanguageMap("en", "error note"),
        };

        var expected = new AuthAccessService2
        {
            Id = "todo",
            Profile = "active",
            Label = new LanguageMap("en", "label"),
            Heading = new LanguageMap("en", "heading"),
            Note = new LanguageMap("en", "note"),
            ConfirmLabel = new LanguageMap("en", "confirm"),
            Service = new List<IService>
            {
                new AuthAccessTokenService2
                {
                    Id = "todo",
                    ErrorHeading = new LanguageMap("en", "error heading"),
                    ErrorNote = new LanguageMap("en", "error note"),
                },
                new AuthLogoutService2
                {
                    Id = "todo",
                    Label = new LanguageMap("en", "logout-label"),
                }
            }
        };

        // Act
        var actual = accessService.ToIIIFModel();
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
}