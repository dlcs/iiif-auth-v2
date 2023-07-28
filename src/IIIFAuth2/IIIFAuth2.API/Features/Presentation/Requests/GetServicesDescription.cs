using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Models.Result;
using IIIFAuth2.API.Utils;
using MediatR;

namespace IIIFAuth2.API.Features.Presentation.Requests;

/// <summary>
/// Request to generate IIIF Services definition for auth services for specified asset + roles  
/// </summary>
public class GetServicesDescription : IRequest<IIIFResourceResponse>
{
    public AssetId AssetId { get; }
    
    public IReadOnlyCollection<string> Roles { get; }

    public GetServicesDescription(string assetId, string roles)
    {
        AssetId = AssetId.FromString(assetId);
        Roles = roles.Split(",", StringSplitOptions.RemoveEmptyEntries);
    }
}

public class GetServicesDescriptionHandler : IRequestHandler<GetServicesDescription, IIIFResourceResponse>
{
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<GetServicesDescriptionHandler> logger;
    private readonly IUrlPathProvider urlPathProvider;

    public GetServicesDescriptionHandler(
        AuthServicesContext dbContext,
        IUrlPathProvider urlPathProvider,
        ILogger<GetServicesDescriptionHandler> logger)
    {
        this.dbContext = dbContext;
        this.urlPathProvider = urlPathProvider;
        this.logger = logger;
    }
    
    public async Task<IIIFResourceResponse> Handle(GetServicesDescription request, CancellationToken cancellationToken)
    {
        var customerId = request.AssetId.Customer;
        var rolesAccessServiceIds = await GetAccessServiceIdsForRoles(request, customerId);
        
        if (rolesAccessServiceIds.IsNullOrEmpty()) return IIIFResourceResponse.NotFound("Requested roles not found");
        
        var accessServices = await GetAccessServices(customerId, rolesAccessServiceIds);
        
        if (accessServices.IsNullOrEmpty())
        {
            logger.LogError("Access services not found, roles {Roles}", request.Roles);
            return IIIFResourceResponse.NotFound("Request access service(s) not found");
        }

        if (accessServices.Count < rolesAccessServiceIds.Count)
        {
            var missing = rolesAccessServiceIds.Except(accessServices.Select(a => a.Id));
            logger.LogWarning("Access services found in Roles table but record not found, ids: {AccessServiceId}",
                missing);
        }

        try
        {
            var probeService = accessServices.ToProbeService(urlPathProvider, request.AssetId);
            return IIIFResourceResponse.Success(probeService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building IIIF services description for {@Request}", request);
            return IIIFResourceResponse.Failure("Error building IIIF services description");
        }
    }
    
    private async Task<ICollection<Guid>> GetAccessServiceIdsForRoles(GetServicesDescription request, int customerId)
    {
        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);

        var accessServiceIds = customerRoles
            .Where(r => request.Roles.Contains(r.Id))
            .Select(r => r.AccessServiceId);
        return accessServiceIds.ToList();
    }

    private async Task<ICollection<AccessService>> GetAccessServices(int customerId, ICollection<Guid> accessServiceIds)
    {
        var customerServices = await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService);
        
        var accessServices = customerServices
            .Where(s => accessServiceIds.Contains(s.Id))
            .ToList();
        return accessServices;
    }
}