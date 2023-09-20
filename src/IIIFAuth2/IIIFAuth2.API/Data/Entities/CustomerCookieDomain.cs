namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// By default cookies are issues for the current domain that the auth service is hosted on (the DLCS domain). This
/// table allows customers to add additional hostnames
/// </summary>
public class CustomerCookieDomain : IHaveCustomer
{
    public int Customer { get; set; }
    
    /// <summary>
    /// List of additional domains that this cookie should be issued for
    /// </summary>
    public List<string> Domains { get; set; } = new();
}