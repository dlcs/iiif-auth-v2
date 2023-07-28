using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// A RoleProvider contains configuration of how to get obtain roles from external provider.
/// </summary>
public class RoleProvider
{
    public Guid Id { get; set; }
    public RoleProviderConfiguration Configuration { get; set; } = null!;

    // ReSharper disable once CollectionNeverUpdated.Global
    public List<AccessService> AccessServices = new();
}