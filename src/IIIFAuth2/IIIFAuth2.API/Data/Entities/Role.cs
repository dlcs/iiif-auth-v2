namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// Maps a DLCS Role to AccessService
/// </summary>
public class Role : IHaveCustomer
{
    /// <summary>
    /// The URI of the role, e.g. https://api.dlcs.io/customers/99/roles/secret
    /// </summary>
    public string Id { get; set; } = null!;
    public int Customer { get; set; }

    /// <summary>
    /// This is the main AccessService for this Role - the one that will be rendered on IIIF manifests 
    /// </summary>
    /// <remarks>
    /// Multiple AccessServices may be able to grant this role - this link is to indicate the primary service to use for
    /// this role.
    /// </remarks>
    public Guid AccessServiceId { get; set; }
    
    public string Name { get; set; } = null!;
}