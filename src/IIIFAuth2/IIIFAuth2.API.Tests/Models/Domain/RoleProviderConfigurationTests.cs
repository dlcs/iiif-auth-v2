using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class RoleProviderConfigurationTests
{
    [Fact]
    public void GetConfiguration_ReturnsDefault_IfNoHostSpecificConfig()
    {
        // Arrange 
        var defaultConfig = new ClickthroughConfiguration { GestureTitle = "test" };
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { RoleProviderConfiguration.DefaultKey, defaultConfig }
        };
        
        // Act
        var actual = roleProviderConfig.GetConfiguration("localhost");
        
        // Assert
        actual.Should().Be(defaultConfig);
    }
    
    [Fact]
    public void GetConfiguration_ReturnsDefault_IfHostSpecificConfigNotFound()
    {
        // Arrange 
        var defaultConfig = new ClickthroughConfiguration { GestureTitle = "test" };
        var hostConfig = new ClickthroughConfiguration { GestureTitle = "anotherTest" };
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { RoleProviderConfiguration.DefaultKey, defaultConfig },
            { "test.example", hostConfig }
        };
        
        // Act
        var actual = roleProviderConfig.GetConfiguration("localhost");
        
        // Assert
        actual.Should().Be(defaultConfig);
    }
    
    [Fact]
    public void GetConfiguration_ReturnsHostSpecific_IfFound()
    {
        // Arrange 
        var defaultConfig = new ClickthroughConfiguration { GestureTitle = "test" };
        var hostConfig = new ClickthroughConfiguration { GestureTitle = "anotherTest" };
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { RoleProviderConfiguration.DefaultKey, defaultConfig },
            { "test.example", hostConfig }
        };
        
        // Act
        var actual = roleProviderConfig.GetConfiguration("test.example");

        // Assert
        actual.Should().Be(hostConfig);
    }

    [Fact]
    public void GetConfiguration_Throws_IfNoHostSpecificConfig_AndNoDefault()
    {
        // Arrange
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { "test.example", new ClickthroughConfiguration() }
        };

        // Act
        Action action = () => roleProviderConfig.GetConfiguration("localhost");

        // Assert
        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("No configuration found for host 'localhost' or 'default'");
    }

    [Fact]
    public void GetConfigurationOrDefault_ReturnsDefault_IfHostSpecificConfigNotFound()
    {
        // Arrange
        var defaultConfig = new ClickthroughConfiguration { GestureTitle = "test" };
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { RoleProviderConfiguration.DefaultKey, defaultConfig },
            { "test.example", new ClickthroughConfiguration { GestureTitle = "anotherTest" } }
        };

        // Act
        var actual = roleProviderConfig.GetConfigurationOrDefault("localhost");

        // Assert
        actual.Should().Be(defaultConfig);
    }

    [Fact]
    public void GetConfigurationOrDefault_ReturnsHostSpecific_IfFound()
    {
        // Arrange
        var hostConfig = new ClickthroughConfiguration { GestureTitle = "anotherTest" };
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { RoleProviderConfiguration.DefaultKey, new ClickthroughConfiguration { GestureTitle = "test" } },
            { "test.example", hostConfig }
        };

        // Act
        var actual = roleProviderConfig.GetConfigurationOrDefault("test.example");

        // Assert
        actual.Should().Be(hostConfig);
    }

    [Fact]
    public void GetConfigurationOrDefault_ReturnsNull_IfNoHostSpecificConfig_AndNoDefault()
    {
        // Arrange
        var roleProviderConfig = new RoleProviderConfiguration
        {
            { "test.example", new ClickthroughConfiguration() }
        };

        // Act
        var actual = roleProviderConfig.GetConfigurationOrDefault("localhost");

        // Assert
        actual.Should().BeNull();
    }
}