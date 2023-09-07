using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Tests.Utils;

public class UriXTests
{
    [Theory]
    [InlineData("https://test.origin", "https://test.origin")]
    [InlineData("https://test.origin/", "https://test.origin")]
    [InlineData("http://test.origin:80/", "http://test.origin")]
    [InlineData("https://test.origin:443/", "https://test.origin")]
    [InlineData("https://test.origin:443/verify/", "https://test.origin")]
    [InlineData("http://test.origin:8080/", "http://test.origin:8080")]
    [InlineData("https://test.origin:8080/postal", "https://test.origin:8080")]
    public void GetOrigin_ReturnsExpected(string uriInput, string expected)
    {
        var uri = new Uri(uriInput);

        var actual = uri.GetOrigin();

        actual.Should().Be(expected);
    }
}