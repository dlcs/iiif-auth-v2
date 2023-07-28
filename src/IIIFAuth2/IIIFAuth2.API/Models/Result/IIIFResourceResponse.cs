using IIIF;

namespace IIIFAuth2.API.Models.Result;

/// <summary>
/// Represents the results of a call to get a IIIF DescriptionResource
/// </summary>
public class IIIFResourceResponse
{
    /// <summary>
    /// Optional representation of entity
    /// </summary>
    public JsonLdBase? DescriptionResource { get; private init;}
    
    /// <summary>
    /// Optional error message if didn't succeed
    /// </summary>
    public string? ErrorMessage { get; private init; }
    
    /// <summary>
    /// If true an error occured fetching resource
    /// </summary>
    public bool Error { get; private init; }
    
    /// <summary>
    /// If true an error occured resources count not be found
    /// </summary>
    public bool EntityNotFound { get; private init; }

    public static IIIFResourceResponse Failure(string? errorMessage)
        => new() { ErrorMessage = errorMessage, Error = true };
    
    public static IIIFResourceResponse NotFound(string? errorMessage = null)
        => new() { ErrorMessage = errorMessage, EntityNotFound = true };

    public static IIIFResourceResponse Success(JsonLdBase entity)
        => new() { DescriptionResource = entity };
}