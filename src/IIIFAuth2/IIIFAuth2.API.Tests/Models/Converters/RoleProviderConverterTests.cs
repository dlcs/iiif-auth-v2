using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace IIIFAuth2.API.Tests.Models.Converters;

public class RoleProviderConverterTests
{
    private readonly RoleProviderConverter sut;
    private readonly JsonSerializerSettings jsonSerialiser;

    public RoleProviderConverterTests()
    {
        sut = new RoleProviderConverter();
        jsonSerialiser = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { sut },
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
    }

    [Fact]
    public void CanDeserialise_Clickthrough()
    {
        // Arrange
        var expected = new ClickthroughConfiguration
        {
            Config = RoleProviderType.Clickthrough,
            GestureMessage = "Gesture message",
            GestureTitle = "Gesture title",
        };

        var json =
            "{\"config\":\"Clickthrough\", \"gestureMessage\":\"Gesture message\", \"gestureTitle\":\"Gesture title\"}";

        // Act
        var actual = JsonConvert.DeserializeObject<IProviderConfiguration>(json, jsonSerialiser);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void CanSerialise_Clickthrough()
    {
        // Arrange
        var config = new ClickthroughConfiguration
        {
            Config = RoleProviderType.Clickthrough,
            GestureMessage = "Gesture message",
            GestureTitle = "Gesture title",
        };

        var json =
            "{\"config\":\"Clickthrough\", \"gestureMessage\":\"Gesture message\", \"gestureTitle\":\"Gesture title\"}";
        var expected = JObject.Parse(json);

        // Act
        var serialised = JsonConvert.SerializeObject(config, Formatting.None, jsonSerialiser);
        
        // Assert
        var actual = JObject.Parse(serialised);

        JToken.DeepEquals(expected, actual).Should().BeTrue($"{actual} should equal {expected}");
    }

    [Fact]
    public void CanDeserialise_Oidc()
    {
        // Arrange
        var expected = new OidcConfiguration
        {
            Config = RoleProviderType.Oidc,
            ClientId = "foobar",
            ClientSecret = "shhhh",
            GestureMessage = "Gesture message",
            GestureTitle = "Gesture title",
            Domain = "my-domain",
            Mapping = new Dictionary<string, string[]>
            {
                ["foo"] = new[] { "http://foo/role" }
            },
            Provider = "auth0",
            ClaimType = "the-main-claim",
            FallbackMapping = new[] { "http://foo/fallback" },
            Scopes = "scoped",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.UseClaim
        };

        var json = @"{
    ""config"": ""Oidc"",
    ""clientId"": ""foobar"",
    ""clientSecret"": ""shhhh"",
    ""provider"": ""auth0"",
    ""domain"": ""my-domain"",
    ""scopes"": ""scoped"",
    ""claimType"": ""the-main-claim"",
    ""mapping"": {
      ""foo"": [""http://foo/role""]
    },
    ""unknownValueBehaviour"": ""UseClaim"",
    ""fallbackMapping"": [""http://foo/fallback""],
    ""gestureMessage"":""Gesture message"",
    ""gestureTitle"":""Gesture title""
}";

        // Act
        var actual = JsonConvert.DeserializeObject<IProviderConfiguration>(json, sut);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void CanSerialise_Oidc()
    {
        // Arrange
        var config = new OidcConfiguration
        {
            Config = RoleProviderType.Oidc,
            ClientId = "foobar",
            ClientSecret = "shhhh",
            GestureMessage = "Gesture message",
            GestureTitle = "Gesture title",
            Domain = "my-domain",
            Mapping = new Dictionary<string, string[]>
            {
                ["foo"] = new[] { "http://foo/role" }
            },
            Provider = "auth0",
            ClaimType = "the-main-claim",
            FallbackMapping = new[] { "http://foo/fallback" },
            Scopes = "scoped",
            UnknownValueBehaviour = UnknownMappingValueBehaviour.UseClaim
        };

        var json = @"{
    ""config"": ""Oidc"",
    ""clientId"": ""foobar"",
    ""clientSecret"": ""shhhh"",
    ""provider"": ""auth0"",
    ""domain"": ""my-domain"",
    ""scopes"": ""scoped"",
    ""claimType"": ""the-main-claim"",
    ""mapping"": {
      ""foo"": [""http://foo/role""]
    },
    ""unknownValueBehaviour"": ""UseClaim"",
    ""fallbackMapping"": [""http://foo/fallback""],
    ""gestureMessage"":""Gesture message"",
    ""gestureTitle"":""Gesture title""
}";
        var expected = JObject.Parse(json);

        // Act
        var serialised = JsonConvert.SerializeObject(config, Formatting.None, jsonSerialiser);
        
        // Assert
        var actual = JObject.Parse(serialised);

        JToken.DeepEquals(expected, actual).Should().BeTrue($"{actual} should equal {expected}");
    }
}