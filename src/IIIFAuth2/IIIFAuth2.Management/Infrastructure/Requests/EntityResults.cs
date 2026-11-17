namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// Result of fetching an entity
/// </summary>
public record FetchEntityResult<T>(T? Entity, bool EntityNotFound = false) where T : class
{
    public static FetchEntityResult<T> Success(T entity) => new(entity);
    public static FetchEntityResult<T> NotFound() => new(null, true);
}