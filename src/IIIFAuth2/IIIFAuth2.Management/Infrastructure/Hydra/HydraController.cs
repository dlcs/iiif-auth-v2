using IIIFAuth2.Management.Infrastructure.Requests;
using IIIFAuth2.Management.Settings;
using MediatR;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Infrastructure.Hydra;

/// <summary>
/// Base class for controllers returning Hydra responses. Slim port of Protagonist API HydraController
/// </summary>
public abstract class HydraController(IMediator mediator, IOptions<ManagementSettings> settings) : ControllerBase
{
    protected readonly IMediator Mediator = mediator;

    /// <summary>
    /// Base url of current request (scheme, host and pathbase), for building "@id" values
    /// </summary>
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    protected async Task<IActionResult> HandleFetch<T>(IRequest<FetchEntityResult<T>> request,
        Func<T, JsonLdBase> hydraBuilder, CancellationToken cancellationToken)
        where T : class
    {
        var result = await Mediator.Send(request, cancellationToken);
        return result.EntityNotFound || result.Entity == null
            ? HydraNotFound()
            : Ok(WithContext(hydraBuilder(result.Entity)));
    }

    protected async Task<IActionResult> HandleUpsert<T>(IRequest<ModifyEntityResult<T>> request,
        Func<T, JsonLdBase> hydraBuilder, string errorTitle, CancellationToken cancellationToken)
        where T : class
    {
        var result = await Mediator.Send(request, cancellationToken);
        switch (result.WriteResult)
        {
            case WriteResult.Created:
                var created = WithContext(hydraBuilder(result.Entity!));
                return Created(created.Id, created);
            case WriteResult.Updated:
                return Ok(WithContext(hydraBuilder(result.Entity!)));
            case WriteResult.NotFound:
                return HydraNotFound(result.Error);
            case WriteResult.FailedValidation:
                return HydraProblem(400, $"{errorTitle}: Validation failed", result.Error);
            case WriteResult.Conflict:
                return HydraProblem(409, $"{errorTitle}: Conflict", result.Error);
            default:
                return HydraProblem(500, errorTitle, result.Error);
        }
    }

    protected async Task<IActionResult> HandleDelete(IRequest<DeleteResult> request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return result == DeleteResult.Deleted ? NoContent() : HydraNotFound();
    }

    /// <summary>
    /// Set paging on request from ?page and ?pageSize, send it and return page as a HydraCollection. Other query
    /// params are carried over to paging links
    /// </summary>
    protected async Task<IActionResult> HandlePagedFetch<TEntity, THydra>(
        IRequest<PageOf<TEntity>> request, Func<TEntity, THydra> hydraBuilder, CancellationToken cancellationToken)
        where THydra : JsonLdBase
    {
        if (request is IPagedRequest pagedRequest) SetPaging(pagedRequest);
        var page = await Mediator.Send(request, cancellationToken);

        var collection = new HydraCollection<THydra>
        {
            Context = JsonLdBase.HydraContext,
            Id = Request.GetDisplayUrl(),
            Members = page.Entities.Select(hydraBuilder).ToArray(),
            TotalItems = page.Total,
            PageSize = page.PageSize,
        };
        PartialCollectionView.AddPaging(collection, page.Page, page.PageSize,
            Request.Query
                .Where(q => q.Key is not ("page" or "pageSize"))
                .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v))));
        return Ok(collection);
    }

    protected ObjectResult HydraProblem(int statusCode, string? title, string? detail)
        => Error.ToResult(statusCode, title, detail, Request.GetDisplayUrl());

    protected ObjectResult HydraNotFound(string? detail = null) => HydraProblem(404, "Not Found", detail);

    private void SetPaging(IPagedRequest pagedRequest)
    {
        pagedRequest.Page = int.TryParse(Request.Query["page"], out var page) && page > 0 ? page : 1;
        pagedRequest.PageSize = int.TryParse(Request.Query["pageSize"], out var pageSize) && pageSize > 0
            ? Math.Min(pageSize, settings.Value.MaxPageSize)
            : settings.Value.DefaultPageSize;
    }

    private static JsonLdBase WithContext(JsonLdBase resource)
    {
        resource.Context = JsonLdBase.HydraContext;
        return resource;
    }
}
