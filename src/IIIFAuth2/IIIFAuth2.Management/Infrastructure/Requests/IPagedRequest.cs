namespace IIIFAuth2.Management.Infrastructure.Requests;

/// <summary>
/// A request for a page of entities, Page and PageSize set from query params by <see cref="Hydra.HydraController"/>
/// </summary>
internal interface IPagedRequest
{
    int Page { get; set; }
    int PageSize { get; set; }
}