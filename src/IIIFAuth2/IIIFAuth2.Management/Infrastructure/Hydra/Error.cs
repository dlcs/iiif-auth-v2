using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.Management.Infrastructure.Hydra;

/// <summary>
/// A Hydra:Error that is also a valid RFC 7807 JSON Problem, matching Protagonist's wire format (the DLCS API client
/// parses these). Hydra "statusCode"/"description" are duplicated as "status"/"detail"
/// </summary>
public class Error() : JsonLdBase("Error")
{
    [JsonPropertyName("type")]
    public string? ErrorTypeUri { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public int Status => StatusCode;

    [JsonPropertyName("detail")]
    public string? Detail => Description;

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    public static ObjectResult ToResult(int statusCode, string? title, string? detail, string? instance)
        => new(new Error { StatusCode = statusCode, Title = title, Description = detail, Instance = instance })
            { StatusCode = statusCode };
}
