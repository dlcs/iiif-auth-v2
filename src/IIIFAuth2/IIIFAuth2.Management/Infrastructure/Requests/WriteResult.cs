namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// Represents the result of a Create or Update operation
/// </summary>
public enum WriteResult
{
    /// <summary>
    /// Entity was successfully created
    /// </summary>
    Created,
    
    /// <summary>
    /// Entity was successfully updated
    /// </summary>
    Updated,
    
    /// <summary>
    /// Source item not found
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Request failed validation
    /// </summary>
    FailedValidation,
    
    /// <summary>
    /// The update values would have resulted in a conflict with an existing resource
    /// </summary>
    Conflict,
}