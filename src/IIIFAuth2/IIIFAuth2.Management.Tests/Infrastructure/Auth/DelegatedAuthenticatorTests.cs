using FakeItEasy;
using IIIFAuth2.Management.Infrastructure.Auth;
using IIIFAuth2.Management.Infrastructure.Dlcs;
using IIIFAuth2.Management.Settings;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Tests.Infrastructure.Auth;

public class DelegatedAuthenticatorTests
{
    private readonly IDlcsApiClient fakeClient;
    private readonly DelegatedAuthenticator sut;

    public DelegatedAuthenticatorTests()
    {
        fakeClient = A.Fake<IDlcsApiClient>();
        A.CallTo(() => fakeClient.IsRequestAuthenticated(10, A<CancellationToken>._)).Returns(true);

        sut = GetSut(new MockCachingService());
    }

    [Fact]
    public async Task ValidateRequest_NoCredentials_IfNoAuthHeader()
    {
        var request = new DefaultHttpContext().Request;

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }

    [Fact]
    public async Task ValidateRequest_NoCredentials_IfAuthHeaderEmpty()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "";

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }

    [Fact]
    public async Task ValidateRequest_NoCredentials_IfNoCustomerIdInRoute()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "Basic 12345";
        request.RouteValues.Add("NotCustomerId", "123");

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }

    [Fact]
    public async Task ValidateRequest_NoCredentials_IfCustomerIdNotNumeric()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "Basic 12345";
        request.RouteValues.Add("customerId", "foo");

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.NoCredentials);
    }

    [Theory]
    [InlineData("Basic 9999")]
    [InlineData("Bearer 9999")]
    public async Task ValidateRequest_Fail_IfDownstreamNon200(string authHeader)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = authHeader;
        request.RouteValues.Add("customerId", "11");

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.Failed);
    }

    [Theory]
    [InlineData("Basic 12345")]
    [InlineData("Bearer 12345")]
    public async Task ValidateRequest_Pass_IfDownstream200(string authHeader)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = authHeader;
        request.RouteValues.Add("customerId", "10");

        var result = await sut.ValidateRequest(request);
        result.Should().Be(AuthResult.Success);
    }

    [Fact]
    public async Task ValidateRequest_CachesSuccessfulResult()
    {
        var cachingSut = GetSut(new CachingService());

        for (var i = 0; i < 2; i++)
        {
            var request = new DefaultHttpContext().Request;
            request.Headers.Authorization = "Basic 12345";
            request.RouteValues.Add("customerId", "10");

            var result = await cachingSut.ValidateRequest(request);
            result.Should().Be(AuthResult.Success);
        }

        A.CallTo(() => fakeClient.IsRequestAuthenticated(10, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private DelegatedAuthenticator GetSut(IAppCache appCache)
        => new(fakeClient, Options.Create(new ManagementSettings()), appCache,
            new NullLogger<DelegatedAuthenticator>());
}
