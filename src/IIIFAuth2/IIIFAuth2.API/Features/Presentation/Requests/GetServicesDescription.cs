using IIIFAuth2.API.Infrastructure.Web;
using IIIFAuth2.API.Models.Converters;
using IIIFAuth2.API.Models.Domain;
using IIIFAuth2.API.Models.Result;
using IIIFAuth2.API.Settings;
using IIIFAuth2.API.Utils;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using MediatR;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Features.Presentation.Requests;

/// <summary>
/// Request to generate IIIF Services definition for auth services for specified deliverable + roles
/// </summary>
public class GetServicesDescription : IRequest<IIIFResourceResponse>
{
    public DeliverableId DeliverableId { get; }

    public IReadOnlyList<RoleEntry> Roles { get; }

    public GetServicesDescription(string deliverableId, string roles)
    {
        DeliverableId = DeliverableId.FromString(deliverableId);
        Roles = RoleEntry.ParseList(roles);
    }
}

public class GetServicesDescriptionHandler : IRequestHandler<GetServicesDescription, IIIFResourceResponse>
{
    private readonly AuthServicesContext dbContext;
    private readonly ILogger<GetServicesDescriptionHandler> logger;
    private readonly IUrlPathProvider urlPathProvider;
    private readonly ApiSettings apiSettings;

    public GetServicesDescriptionHandler(
        AuthServicesContext dbContext,
        IUrlPathProvider urlPathProvider,
        IOptions<ApiSettings> apiOptions,
        ILogger<GetServicesDescriptionHandler> logger)
    {
        this.dbContext = dbContext;
        this.urlPathProvider = urlPathProvider;
        apiSettings = apiOptions.Value;
        this.logger = logger;
    }
    
    public async Task<IIIFResourceResponse> Handle(GetServicesDescription request, CancellationToken cancellationToken)
    {
        var customerId = request.DeliverableId.Customer;
        var customerServices =
            (await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService)).ToList();
        var rolesAccessServiceIds = await GetAccessServiceIdsForRoles(request, customerId, customerServices);
        
        if (rolesAccessServiceIds.IsNullOrEmpty()) return IIIFResourceResponse.NotFound("Requested roles not found");
        
        var accessServices = GetAccessServices(customerServices, rolesAccessServiceIds);
        
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
            var probeService = accessServices.ToProbeService(urlPathProvider, request.DeliverableId,
                apiSettings.UseGenericLocationWording);
            return IIIFResourceResponse.Success(probeService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building IIIF services description for {@Request}", request);
            return IIIFResourceResponse.Failure("Error building IIIF services description");
        }
    }
    
    /// <summary>
    /// Get access services for the requested roles. An external (IP role) service is only included for entries made
    /// entirely of IP roles: in an entry with other roles the user must also act, and the probe explains location
    /// </summary>
    private async Task<ICollection<Guid>> GetAccessServiceIdsForRoles(GetServicesDescription request, int customerId,
        IEnumerable<AccessService> customerServices)
    {
        var customerRoles = await dbContext.Roles.GetCachedCustomerRecords(customerId, CacheKeys.Roles);
        var roleServices = customerRoles.ToDictionary(r => r.Id, r => r.AccessServiceId);
        
        // NOTE - this assumes that all External services are IP based
        var externalIds = customerServices
            .Where(s => s.Profile == AccessServiceProfiles.External)
            .Select(s => s.Id)
            .ToHashSet();

        var ids = new HashSet<Guid>();
        foreach (var entry in request.Roles)
        {
            var entryIds = entry.Roles.Where(roleServices.ContainsKey).Select(r => roleServices[r]).ToList();
            var ipOnly = entryIds.Count == entry.Roles.Count && entryIds.All(externalIds.Contains);
            // in a mixed entry the location alone doesn't grant access, so don't advertise the external service
            ids.UnionWith(ipOnly ? entryIds : entryIds.Where(id => !externalIds.Contains(id)));
        }

        return ids;
    }

    private static List<AccessService> GetAccessServices(IEnumerable<AccessService> customerServices,
        ICollection<Guid> accessServiceIds)
    {
        // external services are output first as some clients only use the first service
        var accessServices = customerServices
            .Where(s => accessServiceIds.Contains(s.Id))
            .OrderByDescending(s => s.Profile == AccessServiceProfiles.External)
            .ToList();
        return accessServices;
    }
}