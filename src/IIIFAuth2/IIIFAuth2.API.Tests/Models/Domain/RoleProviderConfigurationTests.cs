using IIIFAuth2.API.Models.Domain;

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
}