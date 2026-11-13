namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// Result of creating or updating an entity
/// </summary>
public record ModifyEntityResult<T>(WriteResult WriteResult, T? Entity = null, string? Error = null) where T : class
{
    public static ModifyEntityResult<T> Success(T entity, WriteResult result = WriteResult.Updated) => new(result, entity);
    public static ModifyEntityResult<T> Failure(string error, WriteResult result) => new(result, Error: error);
}