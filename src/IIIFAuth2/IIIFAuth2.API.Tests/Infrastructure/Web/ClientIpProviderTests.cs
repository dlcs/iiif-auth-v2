using System.Net;
using FakeItEasy;
using IIIFAuth2.API.Infrastructure.Web;
using Microsoft.AspNetCore.Http;

namespace IIIFAuth2.API.Tests.Infrastructure.Web;

public class ClientIpProviderTests
{
    [Fact]
    public void GetClientIp_ReturnsNull_IfNoHttpContext()
    {
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(null);
        var sut = new ClientIpProvider(httpContextAccessor);

        sut.GetClientIp().Should().BeNull();
    }

    [Fact]
    public void GetClientIp_ReturnsNull_IfNoRemoteIpAddress()
    {
        var sut = GetSut(null);

        sut.GetClientIp().Should().BeNull();
    }

    [Theory]
    [InlineData("10.0.0.1", "10.0.0.1")]
    [InlineData("2001:db8:1007::1", "2001:db8:1007::1")]
    [InlineData("::ffff:10.0.0.1", "10.0.0.1")]
    public void GetClientIp_ReturnsRemoteIpAddress_NormalisingIPv4Mapped(string remoteIp, string expected)
    {
        var sut = GetSut(IPAddress.Parse(remoteIp));

        sut.GetClientIp().Should().Be(IPAddress.Parse(expected));
    }

    private static ClientIpProvider GetSut(IPAddress? remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress;
        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        return new ClientIpProvider(httpContextAccessor);
    }
}
