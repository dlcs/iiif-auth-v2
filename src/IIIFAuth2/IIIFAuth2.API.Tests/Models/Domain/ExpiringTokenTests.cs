using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class ExpiringTokenTests
{
    [Fact]
    public void GenerateNewToken_Throws_IfNonUtcDatePassed()
    {
        Action action = () => ExpiringToken.GenerateNewToken(DateTime.Now);
        action.Should().Throw<ArgumentException>()
            .WithParameterName("timestamp");
    }
    
    [Fact]
    public void GenerateNewToken_CreatesDifferentTokens_ForSametime()
    {
        var datetime = DateTime.UtcNow;
        var token1 = ExpiringToken.GenerateNewToken(datetime);
        var token2 = ExpiringToken.GenerateNewToken(datetime);
        
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void HasExpired_False_ForNewToken()
    {
        var token = ExpiringToken.GenerateNewToken();

        ExpiringToken.HasExpired(token).Should().BeFalse();
    }
    
    [Fact]
    public void HasExpired_True_IfExpired()
    {
        var token = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(-100));

        ExpiringToken.HasExpired(token, 90).Should().BeTrue();
    }
    
    [Fact]
    public void HasExpired_True_ForUnknownFormatToken()
    {
        const string token = "foo";

        ExpiringToken.HasExpired(token).Should().BeTrue();
    }
}