using System.Net;

namespace IIIFAuth2.Management.Infrastructure.Dlcs;

public interface IDlcsApiClient
{
    /// <summary>
    /// Tests to see if the current HTTP request context is authenticated for specified customer
    /// </summary>
    Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IDlcsApiClient"/>
/// </summary>
/// <remarks>Note that this requires <see cref="AmbientAuthHandler"/> to work</remarks>
public class DlcsApiClient(HttpClient httpClient) : IDlcsApiClient
{
    public async Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default)
    {
        var customerPath = $"/customers/{customerId}";
        var response = await httpClient.GetAsync(customerPath, cancellationToken);

        // if DLCS returns 200 then credentials have access
        return response.StatusCode == HttpStatusCode.OK;
    }
}
