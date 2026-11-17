namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// Represents the result of a delete operation
/// </summary>
public enum DeleteResult
{
    /// <summary>
    /// Item was successfully deleted
    /// </summary>
    Deleted,
    
    /// <summary>
    /// Item not deleted as it could not be found
    /// </summary>
    NotFound,
}