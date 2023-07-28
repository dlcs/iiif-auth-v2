using IIIFAuth2.API.Models.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IIIFAuth2.API.Models.Converters;

/// <summary>
/// Polymorphic <see cref="JsonConverter{T}"/> for <see cref="IProviderConfiguration"/> objects 
/// </summary>
public class RoleProviderConverter : JsonConverter<IProviderConfiguration>
{
    public override void WriteJson(JsonWriter writer, IProviderConfiguration? value, JsonSerializer serializer)
    {
        var thisIndex = serializer.Converters.IndexOf(this);
        serializer.Converters.RemoveAt(thisIndex);
        serializer.Serialize(writer, value);
        serializer.Converters.Insert(thisIndex, this);
    }

    public override IProviderConfiguration ReadJson(JsonReader reader, Type objectType, IProviderConfiguration? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);

        var providerConfiguration = CreateConcreteObject(jsonObject);

        serializer.Populate(jsonObject.CreateReader(), providerConfiguration);
        return providerConfiguration;
    }

    private static IProviderConfiguration CreateConcreteObject(JObject jsonObject)
    {
        var configJToken = jsonObject["config"];
        var configValue = configJToken?.ToObject<RoleProviderType>() ?? RoleProviderType.Unknown;

        switch (configValue)
        {
            case RoleProviderType.Clickthrough:
                return new ClickthroughConfiguration();
            case RoleProviderType.Oidc:
                return new OidcConfiguration();
            case RoleProviderType.Unknown:
            default:
                throw new ArgumentOutOfRangeException(
                    $"Could not determine RoleProvider config type for {configJToken ?? "config-key-not-found"}");
        }
    }
}