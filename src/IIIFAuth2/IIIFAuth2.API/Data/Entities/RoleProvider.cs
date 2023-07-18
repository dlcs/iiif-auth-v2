namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// A RoleProvider contains configuration of how to get obtain roles from external provider.
/// </summary>
public class RoleProvider
{
    public Guid Id { get; set; }
    public string Configuration { get; set; } = null!;

    // ReSharper disable once CollectionNeverUpdated.Global
    public List<AccessService> AccessServices = new();
}