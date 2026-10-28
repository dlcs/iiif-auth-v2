using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.Core.Data.Entities;

/// <summary>
/// A RoleProvider contains configuration of how to get obtain roles from external provider.
/// </summary>
public class RoleProvider : IHaveCustomer
{
    public Guid Id { get; set; }
    public int Customer { get; set; }
    public RoleProviderConfiguration Configuration { get; set; } = null!;

    // ReSharper disable once CollectionNeverUpdated.Global
    public List<AccessService> AccessServices = new();
}