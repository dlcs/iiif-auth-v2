﻿using IIIF.Serialisation;
using IIIFAuth2.API.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IIIFAuth2.API.Infrastructure;

public abstract class AuthBaseController : Controller
{
    protected readonly IMediator Mediator;

    /// <inheritdoc />
    protected AuthBaseController(IMediator mediator)
    {
        Mediator = mediator;
    }
    
    protected async Task<IActionResult> HandleRequest(
        Func<IRequest<IIIFResourceResponse>> requestBuilder,
        string contentType = "application/json",
        string errorTitle = "Unhandled Exception",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = requestBuilder();
            var result = await Mediator.Send(request, cancellationToken);

            if (result.Error)
            {
                return Problem(detail: result.ErrorMessage ?? "Error", statusCode: 500, title: errorTitle);
            }

            if (result.EntityNotFound || result.DescriptionResource == null)
            {
                return Problem(detail: result.ErrorMessage ?? "Entity not found", statusCode: 404, title: errorTitle);
            }

            HttpContext.Response.Headers.ContentType = contentType;
            return Ok(result.DescriptionResource.AsJson());
        }
        catch (FormatException fmtEx)
        {
            return Problem(detail: fmtEx.Message, statusCode: 400, title: "Bad Request");
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: 500, title: errorTitle);
        }
    }
}