using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Tests.Utils;

public class StringX
{
    [Fact]
    public void EnsureEndsWith_ReturnsProvidedValue_IfAlreadyEndsWith()
    {
        // Arrange
        const string val = "vultureprince";
        
        // Act
        var actual = val.EnsureEndsWith("ce");
        
        // Assert
        actual.Should().Be(val);
    }
    
    [Fact]
    public void EnsureEndsWith_AppendsValue_IfAlreadyEndsWith()
    {
        // Arrange
        const string val = "vultureprince";
        const string expected = "vultureprince/";
        
        // Act
        var actual = val.EnsureEndsWith("/");
        
        // Assert
        actual.Should().Be(expected);
    }
}