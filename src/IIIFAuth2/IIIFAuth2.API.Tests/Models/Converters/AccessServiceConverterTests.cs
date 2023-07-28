using IIIF;
using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;

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
            Id = "http://test.example/access/test access service",
            Profile = "active",
            Service = new List<IService>
            {
                new AuthAccessTokenService2
                {
                    Id = "http://test.example/token/99",
                },
                new AuthLogoutService2
                {
                    Id = "http://test.example/access/test access service/logout",
                    Label = new LanguageMap("en", "Logout of test access service")
                }
            }
        };

        // Act
        var actual = accessService.ToIIIFModel(new FakePathProvider());
        
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
            Id = "http://test.example/access/test access service",
            Profile = "active",
            Label = new LanguageMap("en", "label"),
            Heading = new LanguageMap("en", "heading"),
            Note = new LanguageMap("en", "note"),
            ConfirmLabel = new LanguageMap("en", "confirm"),
            Service = new List<IService>
            {
                new AuthAccessTokenService2
                {
                    Id = "http://test.example/token/99",
                    ErrorHeading = new LanguageMap("en", "error heading"),
                    ErrorNote = new LanguageMap("en", "error note"),
                },
                new AuthLogoutService2
                {
                    Id = "http://test.example/access/test access service/logout",
                    Label = new LanguageMap("en", "logout-label"),
                }
            }
        };

        // Act
        var actual = accessService.ToIIIFModel(new FakePathProvider());
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ToProbeService_ConvertSingleService()
    {
        // Arrange
        var accessServices = new List<AccessService>
        {
            new()
            {
                Id = Guid.Empty,
                Profile = "active",
                Customer = 99,
                Name = "test access service",
            }
        };

        var expected = new AuthProbeService2
        {
            Id = "http://orchestrator.example/probe/99/10/foo",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://test.example/access/test access service",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = "http://test.example/token/99",
                        },
                        new AuthLogoutService2
                        {
                            Id = "http://test.example/access/test access service/logout",
                            Label = new LanguageMap("en", "Logout of test access service")
                        }
                    }
                }
            }
        };
        
        // Act
        var actual = accessServices.ToProbeService(new FakePathProvider(), new AssetId(99, 10, "foo"));
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void ToProbeService_ConvertMultipleServices()
    {
        // Arrange
        var accessServices = new List<AccessService>
        {
            new()
            {
                Id = Guid.Empty,
                Profile = "active",
                Customer = 99,
                Name = "test access service",
            },
            new()
            {
                Id = Guid.Empty,
                Profile = "active",
                Customer = 99,
                Name = "another service",
            }
        };

        var expected = new AuthProbeService2
        {
            Id = "http://orchestrator.example/probe/99/10/foo",
            Service = new List<IService>
            {
                new AuthAccessService2
                {
                    Id = "http://test.example/access/test access service",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = "http://test.example/token/99",
                        },
                        new AuthLogoutService2
                        {
                            Id = "http://test.example/access/test access service/logout",
                            Label = new LanguageMap("en", "Logout of test access service")
                        }
                    }
                },
                new AuthAccessService2
                {
                    Id = "http://test.example/access/another service",
                    Profile = "active",
                    Service = new List<IService>
                    {
                        new AuthAccessTokenService2
                        {
                            Id = "http://test.example/token/99",
                        },
                        new AuthLogoutService2
                        {
                            Id = "http://test.example/access/another service/logout",
                            Label = new LanguageMap("en", "Logout of another service")
                        }
                    }
                }
            }
        };
        
        // Act
        var actual = accessServices.ToProbeService(new FakePathProvider(), new AssetId(99, 10, "foo"));
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    private class FakePathProvider : IUrlPathProvider
    {
        public Uri GetOrchestratorProbeServicePath(AssetId assetId)
            => new($"http://orchestrator.example/probe/{assetId}");

        public Uri GetAccessServicePath(AccessService accessService)
            => new($"http://test.example/access/{accessService.Name}");

        public Uri GetAccessServiceLogoutPath(AccessService accessService)
            => new($"http://test.example/access/{accessService.Name}/logout");

        public Uri GetAccessTokenServicePath(int customer)
            => new($"http://test.example/token/{customer}");
    }
}