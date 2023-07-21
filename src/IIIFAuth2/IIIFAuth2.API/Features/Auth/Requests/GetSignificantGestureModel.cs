using IIIFAuth2.API.Data;
using IIIFAuth2.API.Data.Entities;
using IIIFAuth2.API.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.API.Features.Auth.Requests;

/// <summary>
/// Generate model to allow significant gesture model to be rendered
/// </summary>
public class GetSignificantGestureModel : IRequest<SignificantGestureModel?>
{
    public int CustomerId { get; }
    public string AccessServiceName { get; }
    public GetSignificantGestureModel(int customerId, string accessServiceName)
    {
        CustomerId = customerId;
        AccessServiceName = accessServiceName;
    }
}

public class GetSignificantGestureModelHandler : IRequestHandler<GetSignificantGestureModel, SignificantGestureModel?>
{
    private readonly AuthServicesContext dbContext;
    private readonly ApiSettings apiSettings;
    private readonly ILogger<GetSignificantGestureModelHandler> logger;
    
    public GetSignificantGestureModelHandler(
        AuthServicesContext dbContext,
        IOptions<ApiSettings> apiOptions,
        ILogger<GetSignificantGestureModelHandler> logger)
    {
        this.dbContext = dbContext;
        apiSettings = apiOptions.Value;
        this.logger = logger;
    }
    
    public async Task<SignificantGestureModel?> Handle(GetSignificantGestureModel request, CancellationToken cancellationToken)
    {
        var roleProvider = await GetRoleProviderForAccessService(request);

        if (roleProvider == null) return null;

        var configuration = roleProvider.Configuration.GetDefaultConfiguration();
        return new SignificantGestureModel(
            configuration.GestureTitle ?? apiSettings.DefaultSignificantGestureTitle,
            configuration.GestureMessage ?? apiSettings.DefaultSignificantGestureMessage);
    }

    private async Task<RoleProvider?> GetRoleProviderForAccessService(GetSignificantGestureModel request)
    {
        var accessService = await GetAccessServices(request.CustomerId, request.AccessServiceName);
        if (accessService == null) return null;

        var roleProvider = accessService.RoleProvider;
        if (roleProvider == null)
        {
            logger.LogWarning(
                "AccessService '{AccessServiceId}' ({CustomerId}:{AccessServiceName}) has no RoleProvider",
                accessService.Id, request.CustomerId, accessService.Name);
            return roleProvider;
        }

        return roleProvider;
    }

    private async Task<AccessService?> GetAccessServices(int customerId, string accessServiceName)
    {
        var customerServices = await dbContext.AccessServices.GetCachedCustomerRecords(customerId, CacheKeys.AccessService);

        var accessService = customerServices
            .SingleOrDefault(s => s.Name.Equals(accessServiceName, StringComparison.OrdinalIgnoreCase));
        return accessService;
    }
}

public record SignificantGestureModel(string SignificantGestureTitle, string SignificantGestureMessage);