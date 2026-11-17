namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// A page of entities, with total count of all matching entities
/// </summary>
public record PageOf<T>(IReadOnlyList<T> Entities, int Total, int Page, int PageSize);