using System.Net;
using Amazon.SecretsManager;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using FakeItEasy;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Appointments;
using IIIFAuth2.API.Infrastructure.Auth.RoleProvisioning.Oidc;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Tests.TestingInfrastructure;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Tests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IIIFAuth2.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class AccessServiceTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly HttpClient httpClient;
    private readonly AuthServicesContext dbContext;
    private readonly DatabaseFixture dbFixture;
    private static readonly IOAuthClient AuthClient = A.Fake<IOAuthClient>();

    public AccessServiceTests(AuthWebApplicationFactory factory, DatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        this.dbFixture = dbFixture;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services => 
                services
                    .AddSingleton(A.Fake<IAmazonSecretsManager>())
                    .AddScoped<IOAuthClient>(_ => AuthClient))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task AccessService_Returns400_IfOriginNotProvided()
    {
        // Arrange
        const string path = "/access/1/foo";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task AccessService_Returns404_IfAccessServiceNotFound()
    {
        // Arrange
        const string path = "/access/1/foo?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersSignificantGestureView_IfDifferentHost()
    {
        // Arrange
        const string path = "/access/99/clickthrough?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p") as IHtmlParagraphElement;
        label.TextContent.Should().Be("Test-Message");
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersSignificantGestureView_WithRoleProvisionToken_IfDifferentHost()
    {
        // Arrange
        const string path = "/access/99/clickthrough?origin=http://whatever.here";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());

        var hidden = document.QuerySelector("form>#SingleUseToken") as IHtmlInputElement;
        var hiddenValue = hidden!.Value;
        
        ExpiringToken.HasExpired(hiddenValue).Should().BeFalse("A valid expiring token is returned");

        var token = await dbContext.RoleProvisionTokens.SingleAsync(t => t.Id == hiddenValue);
        token.Roles.Should().ContainSingle(DatabaseFixture.ClickthroughRoleUri);
        token.Origin.Should().Be("http://whatever.here/");
        token.Used.Should().BeFalse();
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfSameHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin={httpClient.BaseAddress}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfSameHost_ObeysXForwardedProto()
    {
        // Arrange
        var baseAddress = httpClient.BaseAddress!.ToString().Replace("http://", "https://");
        var path = $"/access/99/clickthrough?origin={baseAddress}";
            
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("x-forwarded-proto", "https");
        var response = await httpClient.SendAsync(request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfDifferentHost_ButMatchesCookieHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin=https://{DatabaseFixture.CookieDomain}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_CreatesSessionAndSetsCookie_IfDifferentHost_ButSubdomainOfCookieHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin=https://iiif.{DatabaseFixture.CookieDomain}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Clickthrough_RendersWindowClose_IfSameHost()
    {
        // Arrange
        var path = $"/access/99/clickthrough?origin={httpClient.BaseAddress}";
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }

    [Fact]
    public async Task AccessService_IpRestriction_RendersWindowClose()
    {
        // Arrange
        var path = $"/access/99/{DatabaseFixture.IpRestrictionService}?origin={httpClient.BaseAddress}";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
        (await dbContext.SessionUsers.AnyAsync()).Should().BeFalse("no session created for IpRestriction");
    }

    [Fact]
    public async Task AccessService_Appointment_RendersAppointmentKeyForm()
    {
        // Arrange
        var origin = httpClient.BaseAddress!.ToString();
        var path = $"/access/99/{DatabaseFixture.AppointmentService}?origin={origin}";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        document.Title.Should().Be(DatabaseFixture.AppointmentGestureTitle);
        document.QuerySelector("p")!.TextContent.Should().Be(DatabaseFixture.AppointmentGestureMessage);
        document.QuerySelector(".error").Should().BeNull();

        var form = document.QuerySelector("form") as IHtmlFormElement;
        form!.Method.Should().BeEquivalentTo("post");
        form.Action.Should().EndWith($"/auth/v2/access/99/{DatabaseFixture.AppointmentService}");
        (form.QuerySelector("input[name=appointmentKey]") as IHtmlInputElement)!.Type.Should().Be("text");
        (form.QuerySelector("input[name=origin]") as IHtmlInputElement)!.Value.Should().Be(origin);

        (await dbContext.SessionUsers.AnyAsync()).Should().BeFalse("session is only created when key is posted");
    }

    [Fact]
    public async Task AccessService_Oidc_ReturnsRedirectToLoginUrl()
    {
        // Arrange
        var path = $"/access/99/oidc?origin={httpClient.BaseAddress}";
        var authUri = new Uri("http://sample.idp/authorize");
        A.CallTo(() => AuthClient.GetAuthLoginUrl(A<OidcConfiguration>._, A<AccessService>._, A<string>._, A<CancellationToken>._))
            .Returns(authUri);
        
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(authUri);
    }

    [Fact]
    public async Task AccessService_OAuth2Callback_Returns404_IfAccessServiceNotFound()
    {
        // Arrange
        const string path = "/access/99/foo/oauth2/callback?state=value&code=12345";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task AccessService_OAuth2Callback_Returns_WindowClose_IfStateUnknown()
    {
        // Arrange
        const string path = "/access/99/oidc/oauth2/callback?state=value&code=12345";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }
    
    [Fact]
    public async Task AccessService_OAuth2Callback_Returns_WindowClose_IfStateUsed()
    {
        // Arrange
        var token = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(DateTime.UtcNow), Customer = 99, Used = true, Origin = "http://wherever"
        };
        await dbContext.RoleProvisionTokens.AddAsync(token);
        await dbContext.SaveChangesAsync();
        var path = $"/access/99/oidc/oauth2/callback?state={Uri.EscapeDataString(token.Id)}&code=12345";
            
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }
    
    [Fact]
    public async Task AccessService_OAuth2Callback_RendersSignificantGestureView_MarksStateTokenAsUsed()
    {
        // Arrange
        var stateToken = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(DateTime.UtcNow), Customer = 99, Used = false, Origin = "http://whatever.here"
        };
        await dbContext.RoleProvisionTokens.AddAsync(stateToken);
        await dbContext.SaveChangesAsync();
        const string code =
            nameof(AccessService_OAuth2Callback_RendersSignificantGestureView_WithRoleProvisionToken_IfDifferentHost);
        var path = $"/access/99/oidc/oauth2/callback?state={Uri.EscapeDataString(stateToken.Id)}&code={code}";
        A.CallTo(() =>
                AuthClient.GetDlcsRolesForCode(A<OidcConfiguration>._, A<AccessService>._, code,
                    A<CancellationToken>._))
            .Returns(new List<string> { DatabaseFixture.OidcRoleUri });
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await dbContext.Entry(stateToken).ReloadAsync();
        stateToken.Used.Should().BeTrue();
    }
    
    [Fact]
    public async Task AccessService_OAuth2Callback_RendersSignificantGestureView_WithRoleProvisionToken_IfDifferentHost()
    {
        // Arrange
        var stateToken = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(DateTime.UtcNow), Customer = 99, Used = false, Origin = "http://whatever.here"
        };
        await dbContext.RoleProvisionTokens.AddAsync(stateToken);
        await dbContext.SaveChangesAsync();
        const string code =
            nameof(AccessService_OAuth2Callback_RendersSignificantGestureView_WithRoleProvisionToken_IfDifferentHost);
        var path = $"/access/99/oidc/oauth2/callback?state={Uri.EscapeDataString(stateToken.Id)}&code={code}";
        A.CallTo(() =>
                AuthClient.GetDlcsRolesForCode(A<OidcConfiguration>._, A<AccessService>._, code,
                    A<CancellationToken>._))
            .Returns(new List<string> { DatabaseFixture.OidcRoleUri });
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());

        var hidden = document.QuerySelector("form>#SingleUseToken") as IHtmlInputElement;
        var hiddenValue = hidden!.Value;
        
        ExpiringToken.HasExpired(hiddenValue).Should().BeFalse("A valid expiring token is returned");

        var token = await dbContext.RoleProvisionTokens.SingleAsync(t => t.Id == hiddenValue);
        token.Roles.Should().ContainSingle(DatabaseFixture.ClickthroughRoleUri);
        token.Origin.Should().Be("http://whatever.here/");
        token.Used.Should().BeFalse();
    }
    
    [Fact]
    public async Task AccessService_OAuth2Callback_CreatesSessionAndSetsCookie_IfSameHost()
    {
        // Arrange
        var stateToken = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(DateTime.UtcNow), Customer = 99, Used = false, Origin = httpClient.BaseAddress.ToString()
        };
        await dbContext.RoleProvisionTokens.AddAsync(stateToken);
        await dbContext.SaveChangesAsync();
        const string code = nameof(AccessService_OAuth2Callback_CreatesSessionAndSetsCookie_IfSameHost);
        var path = $"/access/99/oidc/oauth2/callback?state={Uri.EscapeDataString(stateToken.Id)}&code={code}";
        A.CallTo(() =>
                AuthClient.GetDlcsRolesForCode(A<OidcConfiguration>._, A<AccessService>._, code,
                    A<CancellationToken>._))
            .Returns(new List<string> { DatabaseFixture.OidcRoleUri });
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
    }
    
    [Fact]
    public async Task AccessService_Oidc_RendersWindowClose_IfSameHost()
    {
        // Arrange
        var stateToken = new RoleProvisionToken
        {
            Id = ExpiringToken.GenerateNewToken(DateTime.UtcNow), Customer = 99, Used = false, Origin = httpClient.BaseAddress.ToString()
        };
        await dbContext.RoleProvisionTokens.AddAsync(stateToken);
        await dbContext.SaveChangesAsync();
        const string code = nameof(AccessService_Oidc_RendersWindowClose_IfSameHost);
        var path = $"/access/99/oidc/oauth2/callback?state={Uri.EscapeDataString(stateToken.Id)}&code={code}";
        A.CallTo(() =>
                AuthClient.GetDlcsRolesForCode(A<OidcConfiguration>._, A<AccessService>._, code,
                    A<CancellationToken>._))
            .Returns(new List<string> { DatabaseFixture.OidcRoleUri });
            
        // Act
        var response = await httpClient.GetAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }

    [Fact]
    public async Task SignificantGesture_Returns400_IfNoSingleUseToken()
    {
        // Arrange
        const string path = "/access/99/gesture";
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>()
        });
        var response = await httpClient.PostAsync(path, formContent);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenExpired()
    {
        // Arrange
        const string path = "/access/99/gesture";
        var expiredToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(-1));
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", expiredToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenValidButNotInDatabase()
    {
        // Arrange
        const string path = "/access/99/gesture";
        var expiredToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", expiredToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_RendersWindowClose_WithError_IfTokenValidButUsed()
    {
        // Arrange
        const string path = "/access/99/gesture";
        var validToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
        await dbContext.RoleProvisionTokens.AddAsync(CreateToken(validToken, true, Array.Empty<string>()));
        await dbContext.SaveChangesAsync();
            
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", validToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        await ValidateWindowCloseWithError(response);
    }
    
    [Fact]
    public async Task SignificantGesture_CreatesSession_AndSetsCookie_AndMarksTokenAsUsed()
    {
        // Arrange
        const string path = "/access/99/gesture";
        var validToken = ExpiringToken.GenerateNewToken(DateTime.UtcNow.AddHours(1));
        var roles = new string[] { DatabaseFixture.ClickthroughRoleUri };
        var tokenEntity = await dbContext.RoleProvisionTokens.AddAsync(CreateToken(validToken, false, roles));
        var beforeVersion = tokenEntity.Entity.Version;
        await dbContext.SaveChangesAsync();
        
        // Act
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("singleUseToken", validToken)
        });
        var response = await httpClient.PostAsync(path, formContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.Single(header => header.Key == "Set-Cookie").Value.First();
        cookie.Should()
            .StartWith("dlcs-auth2-99")
            .And.Contain("samesite=none")
            .And.Contain("secure;")
            .And.Contain("httponly");
        
        // E.g. dlcs-token-99=id%3D76e7d9fb-99ab-4b4f-87b0-f2e3f0e9664e; expires=Tue, 14 Sep 2021 16:53:53 GMT; domain=localhost; path=/; secure; samesite=none
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        var cookieId = cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
        
        var authToken = await dbContext.SessionUsers.SingleAsync(at => at.CookieId == cookieId);
        authToken.Expires.Should().NotBeBefore(DateTime.UtcNow);
        authToken.Customer.Should().Be(99);
        authToken.Roles.Should().BeEquivalentTo(roles);
        authToken.Origin.Should().Be("http://localhost");
        
        await dbContext.Entry(tokenEntity.Entity).ReloadAsync();
        tokenEntity.Entity.Used.Should().BeTrue("token is now used");
        tokenEntity.Entity.Version.Should().NotBe(beforeVersion);
    }

    [Fact]
    public async Task Logout_Returns204_IfNoCookieProvided()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        
        // Act
        var response = await httpClient.GetAsync(path);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_IfCookieProvided_ButInvalidFormat()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=unexpected-value;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_IfCookieProvidedWithId_ButIdNotInDatabase()
    {
        // Arrange
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", "dlcs-auth2-99=id=123456789;");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task Logout_Returns204_AndMarksAsExpired_IfCookieValid_AndInDatabase()
    {
        // Arrange
        const string cookieId = nameof(Logout_Returns204_AndMarksAsExpired_IfCookieValid_AndInDatabase);
        var sessionUser =
            await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId, expires: DateTime.UtcNow.AddHours(1)));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await dbContext.Entry(sessionUser.Entity).ReloadAsync();
        sessionUser.Entity.LastChecked.Should().HaveValue();
        sessionUser.Entity.Expires.Should().BeBefore(DateTime.UtcNow);
    }
    
    [Fact]
    public async Task Logout_SetsExpiredCookie_IfCookieValid()
    {
        // Arrange
        const string cookieId = nameof(Logout_SetsExpiredCookie_IfCookieValid);
        await dbContext.SessionUsers.AddAsync(CreateSessionUser(cookieId));
        await dbContext.SaveChangesAsync();
        
        const string path = "/access/99/clickthrough/logout";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        
        // Act
        var response = await httpClient.SendAsync(request);
            
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        response.Headers.Should().ContainKey("Set-Cookie");
        var cookie = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value.First();

        var expires = DateTime.Parse(cookie.Split(';').Single(c => c.Trim().StartsWith("expires")).Split('=')[1]);
        expires.Should().BeBefore(DateTime.UtcNow);
    }

    private static async Task ValidateWindowCloseWithError(HttpResponseMessage response)
    {
        var htmlParser = new HtmlParser();
        var document = htmlParser.ParseDocument(await response.Content.ReadAsStreamAsync());
        var err = document.QuerySelector("p") as IHtmlParagraphElement;
        err.TextContent.Should().Be("Token invalid or expired");
        var label = document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement;
        label.TextContent.Should().Be("This window should close automatically...");
    }

    private static RoleProvisionToken CreateToken(string token, bool used, string[] roles)
        => new()
        {
            Id = token, Created = DateTime.UtcNow, Customer = 99, Roles = roles.ToList(), Used = used,
            Origin = "http://localhost"
        };
    
    private static SessionUser CreateSessionUser(string cookieId, int customer = 99, DateTime? expires = null)
        => new()
        {
            Id = Guid.NewGuid(),
            CookieId = cookieId,
            AccessToken = "found-access-token",
            Customer = 99,
            Created = DateTime.UtcNow,
            Roles = new List<string> { "foo" },
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Origin = "http://localhost/",
            LastChecked = null
        };
    [Fact]
    public async Task AppointmentKey_Returns404_IfAccessServiceNotFound()
    {
        var response = await PostAppointmentKey("foo", "a5rf65ne99pa");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AppointmentKey_Returns404_IfAccessServiceNotAppointment()
    {
        var response = await PostAppointmentKey(DatabaseFixture.ClickthroughService, "a5rf65ne99pa");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await dbContext.SessionUsers.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AppointmentKey_ValidKey_NoCookie_CreatesSessionWithAppointment()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_ValidKey_NoCookie_CreatesSessionWithAppointment));

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, $"  {appointment.Token} ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCloseWindow(response);

        var cookieId = GetCookieId(response);
        var session = await dbContext.SessionUsers.AsNoTracking().SingleAsync(su => su.CookieId == cookieId);
        session.Roles.Should().BeEmpty("appointment roles are never stored on session");
        session.Origin.Should().Be(httpClient.BaseAddress!.ToString());
        var sessionAppointment = await dbContext.SessionAppointments.AsNoTracking().SingleAsync();
        sessionAppointment.SessionUserId.Should().Be(session.Id);
        sessionAppointment.AppointmentId.Should().Be(appointment.Id);
    }

    [Fact]
    public async Task AppointmentKey_ValidKey_ExistingSession_AddsAppointmentToSession()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_ValidKey_ExistingSession_AddsAppointmentToSession));
        var existing = await CreateSession([DatabaseFixture.ClickthroughRoleUri]);

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token,
            existing.CookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCloseWindow(response);
        GetCookieId(response).Should().Be(existing.CookieId, "cookie re-issued for existing session");

        var sessions = await dbContext.SessionUsers.AsNoTracking().ToListAsync();
        sessions.Should().ContainSingle().Which.Roles.Should().BeEquivalentTo(DatabaseFixture.ClickthroughRoleUri);
        var sessionAppointment = await dbContext.SessionAppointments.AsNoTracking().SingleAsync();
        sessionAppointment.SessionUserId.Should().Be(existing.Id);
        sessionAppointment.AppointmentId.Should().Be(appointment.Id);
    }

    [Fact]
    public async Task AppointmentKey_ValidKey_ExistingSessionAlreadyHasAppointment_NoDuplicate()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_ValidKey_ExistingSessionAlreadyHasAppointment_NoDuplicate));
        var existing = await CreateSession([], appointment.Id);

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token,
            existing.CookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCloseWindow(response);
        (await dbContext.SessionUsers.CountAsync()).Should().Be(1);
        (await dbContext.SessionAppointments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AppointmentKey_ValidKey_ExpiredSession_CreatesNewSession()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_ValidKey_ExpiredSession_CreatesNewSession));
        var existing = await CreateSession([DatabaseFixture.ClickthroughRoleUri],
            expires: DateTime.UtcNow.AddMinutes(-1));

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token,
            existing.CookieId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookieId = GetCookieId(response);
        cookieId.Should().NotBe(existing.CookieId);
        var newSession = await dbContext.SessionUsers.AsNoTracking().SingleAsync(su => su.CookieId == cookieId);
        (await dbContext.SessionAppointments.AsNoTracking().SingleAsync()).SessionUserId.Should().Be(newSession.Id);
    }

    [Theory]
    [InlineData("unknown-key")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AppointmentKey_UnknownKey_RendersFormWithError(string key)
    {
        // Arrange
        await CreateAppointment(nameof(AppointmentKey_UnknownKey_RendersFormWithError));

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, key);

        // Assert
        await AssertFormWithError(response, AppointmentProviderHandler.UnknownKeyError);
    }

    [Fact]
    public async Task AppointmentKey_KeyForDifferentCustomer_RendersFormWithError()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_KeyForDifferentCustomer_RendersFormWithError),
            a => a.Customer = 100);

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token);

        // Assert
        await AssertFormWithError(response, AppointmentProviderHandler.UnknownKeyError);
    }

    [Fact]
    public async Task AppointmentKey_EndedAppointment_RendersFormWithError()
    {
        // Arrange
        var appointment = await CreateAppointment(nameof(AppointmentKey_EndedAppointment_RendersFormWithError),
            a => a.End = DateTime.UtcNow.AddMinutes(-1));

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token);

        // Assert
        await AssertFormWithError(response, AppointmentProviderHandler.EndedError);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AppointmentKey_NotStartedOrInactive_Accepted(bool notStarted, bool inactive)
    {
        // Arrange
        var appointment = await CreateAppointment($"{nameof(AppointmentKey_NotStartedOrInactive_Accepted)}{notStarted}",
            a =>
            {
                if (notStarted) a.Start = DateTime.UtcNow.AddDays(1);
                if (inactive) a.Active = false;
            });

        // Act
        var response = await PostAppointmentKey(DatabaseFixture.AppointmentService, appointment.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCloseWindow(response);
        (await dbContext.SessionAppointments.AsNoTracking().SingleAsync()).AppointmentId.Should().Be(appointment.Id);
    }

    private Task<HttpResponseMessage> PostAppointmentKey(string accessService, string key, string? cookieId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/access/99/{accessService}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["appointmentKey"] = key,
                ["origin"] = httpClient.BaseAddress!.ToString(),
            })
        };
        if (cookieId != null) request.Headers.Add("Cookie", $"dlcs-auth2-99=id={cookieId};");
        return httpClient.SendAsync(request);
    }

    private async Task<Appointment> CreateAppointment(string token, Action<Appointment>? configure = null)
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            Customer = DatabaseFixture.Customer,
            RoleProviderId = dbFixture.AppointmentRoleProviderId,
            Token = token,
            Roles = [DatabaseFixture.AppointmentRoleUri],
            Start = DateTime.UtcNow.AddHours(-1),
            End = DateTime.UtcNow.AddHours(1),
            Active = true,
        };
        configure?.Invoke(appointment);
        await using var context = dbFixture.CreateNewAuthServiceContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        return appointment;
    }

    private async Task<SessionUser> CreateSession(List<string> roles, Guid? appointmentId = null,
        DateTime? expires = null)
    {
        var session = new SessionUser
        {
            Id = Guid.NewGuid(),
            Customer = DatabaseFixture.Customer,
            CookieId = Guid.NewGuid().ToString(),
            AccessToken = Guid.NewGuid().ToString("N"),
            Origin = httpClient.BaseAddress!.ToString(),
            Roles = roles,
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            LastChecked = DateTime.UtcNow,
        };
        if (appointmentId.HasValue)
            session.SessionAppointments.Add(new SessionAppointment { AppointmentId = appointmentId.Value });
        await using var context = dbFixture.CreateNewAuthServiceContext();
        context.SessionUsers.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    private static string GetCookieId(HttpResponseMessage response)
    {
        var cookie = response.Headers.Single(header => header.Key == "Set-Cookie").Value.First();
        var toRemoveLength = "dlcs-auth2-99id%3D".Length;
        return cookie.Substring(toRemoveLength + 1, cookie.IndexOf(';') - toRemoveLength - 1);
    }

    private static async Task AssertCloseWindow(HttpResponseMessage response)
    {
        var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStreamAsync());
        document.QuerySelector("form").Should().BeNull();
        (document.QuerySelector("p:nth-child(2)") as IHtmlParagraphElement)!.TextContent
            .Should().Be("This window should close automatically...");
    }

    private async Task AssertFormWithError(HttpResponseMessage response, string error)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContainKey("Set-Cookie");
        var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStreamAsync());
        document.QuerySelector(".error")!.TextContent.Should().Be(error);
        document.QuerySelector("form").Should().NotBeNull();
        (await dbContext.SessionUsers.AnyAsync()).Should().BeFalse();
    }
}
