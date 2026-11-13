using System.Text.Json;
using System.Text.Json.Serialization;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Models.Api;

/// <summary>
/// Reads role entries from an array whose elements are either a string array (one entry, all roles required) or a
/// string (one entry, parsed with <see cref="RoleEntry.Parse"/> so legacy flat <c>string[]</c> bodies are OR).
/// Empty entries are dropped
/// </summary>
public class RoleEntriesJsonConverter : JsonConverter<RoleEntry[]>
{
    public override RoleEntry[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("roles must be an array");

        var entries = new List<RoleEntry>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var entry = reader.TokenType switch
            {
                JsonTokenType.String => RoleEntry.Parse(reader.GetString()!),
                JsonTokenType.StartArray => ReadEntry(ref reader),
                _ => throw new JsonException("roles elements must be a string or array of strings"),
            };
            if (entry.Roles.Count > 0) entries.Add(entry);
        }

        return entries.ToArray();
    }

    private static RoleEntry ReadEntry(ref Utf8JsonReader reader)
    {
        var roles = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("role must be a string");
            var role = reader.GetString()!.Trim();
            if (role.Length > 0 && !roles.Contains(role)) roles.Add(role);
        }

        return new RoleEntry(roles);
    }

    /// <remarks>Request body is only ever read</remarks>
    public override void Write(Utf8JsonWriter writer, RoleEntry[] value, JsonSerializerOptions options)
        => throw new NotSupportedException();
}
