using System.Net;
using FakeItEasy;
using IIIFAuth2.API.Infrastructure.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Tests.Infrastructure.Web;

public class ClientIpProviderTests
{
    private static readonly IPAddress ProxyIp = IPAddress.Parse("172.16.0.10");
    private const string ProxyNetwork = "172.16.0.0/12";

    [Fact]
    public void GetClientIp_Throws_IfNoHttpContext()
    {
        var httpContextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => httpContextAccessor.HttpContext).Returns(null);
        var sut = new ClientIpProvider(httpContextAccessor, Options.Create(CreateOptions()),
            NullLogger<ClientIpProvider>.Instance);

        Action action = () => sut.GetClientIp();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetClientIp_ReturnsNull_IfReceivedForwardedForNotRecorded()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName] = "10.0.0.2";

        GetSut(context, CreateOptions()).GetClientIp().Should().BeNull();
    }

    [Fact]
    public async Task GetClientIp_ReturnsNull_IfNoForwardedFor()
    {
        var options = CreateOptions();
        var context = await ProcessRequest(null, options);

        context.Connection.RemoteIpAddress.Should().Be(ProxyIp, "caller IP is not changed");
        GetSut(context, options).GetClientIp().Should().BeNull();
    }

    [Fact]
    public async Task GetClientIp_ReturnsNull_IfForwardedForFromUnknownProxy()
    {
        var options = CreateOptions(knownNetwork: "192.168.0.0/16");
        var context = await ProcessRequest("10.0.0.1", options);

        context.Connection.RemoteIpAddress.Should().Be(ProxyIp, "caller IP is not changed");
        GetSut(context, options).GetClientIp().Should().BeNull();
    }

    [Theory]
    [InlineData("10.0.0.1", "10.0.0.1")]
    [InlineData("2001:db8:1007::1", "2001:db8:1007::1")]
    [InlineData("::ffff:10.0.0.1", "10.0.0.1")]
    [InlineData("1.2.3.4, 10.0.0.1", "10.0.0.1")]
    public async Task GetClientIp_ReturnsForwardedForIp_IfFromKnownProxy_NormalisingIPv4Mapped(string forwardedFor,
        string expected)
    {
        var options = CreateOptions();
        var context = await ProcessRequest(forwardedFor, options);

        GetSut(context, options).GetClientIp().Should().Be(IPAddress.Parse(expected));
    }

    [Fact]
    public async Task GetClientIp_ReturnsForwardedForIp_IfFromKnownNetwork_AndNotKnownProxy()
    {
        var options = CreateOptions(knownNetwork: ProxyNetwork);
        var context = await ProcessRequest("203.0.113.7", options);

        GetSut(context, options).GetClientIp().Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Theory]
    [InlineData("172.16.5.5")]
    [InlineData("::ffff:172.16.5.5")]
    [InlineData("203.0.113.7, 172.16.5.5")]
    public async Task GetClientIp_ReturnsNull_IfResolvedIpInKnownNetworks(string forwardedFor)
    {
        // e.g. proxy (Orchestrator) called via an ALB without x-forwarded-for, so ALB adds proxy IP. ForwardLimit=1
        // so only rightmost entry is applied
        var options = CreateOptions(knownNetwork: ProxyNetwork);
        var context = await ProcessRequest(forwardedFor, options);

        context.Connection.RemoteIpAddress!.MapToIPv4().Should().Be(IPAddress.Parse("172.16.5.5"),
            "forwarded headers middleware applied x-forwarded-for");
        GetSut(context, options).GetClientIp().Should().BeNull();
    }

    [Theory]
    [InlineData("52.18.0.10")]
    [InlineData("::ffff:52.18.0.10")]
    public async Task GetClientIp_ReturnsNull_IfResolvedIpIsKnownProxy(string knownProxy)
    {
        var options = CreateOptions(knownNetwork: ProxyNetwork);
        options.KnownProxies.Add(IPAddress.Parse(knownProxy));
        var context = await ProcessRequest("52.18.0.10", options);

        GetSut(context, options).GetClientIp().Should().BeNull();
    }

    private static ForwardedHeadersOptions CreateOptions(string? knownNetwork = null)
    {
        // As configured in app startup. No knownNetwork is "AllNetworks"
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        if (knownNetwork != null) options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(knownNetwork));
        return options;
    }

    /// <summary>
    /// Run request through recording and forwarded headers middleware, as configured in app startup
    /// </summary>
    private static async Task<HttpContext> ProcessRequest(string? forwardedFor, ForwardedHeadersOptions options)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ProxyIp;
        if (forwardedFor != null)
        {
            context.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName] = forwardedFor;
        }

        ClientIpProvider.RecordReceivedForwardedFor(context);
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(options));
        await middleware.Invoke(context);
        return context;
    }

    private static ClientIpProvider GetSut(HttpContext context, ForwardedHeadersOptions options)
        => new(new HttpContextAccessor { HttpContext = context }, Options.Create(options),
            NullLogger<ClientIpProvider>.Instance);
}
