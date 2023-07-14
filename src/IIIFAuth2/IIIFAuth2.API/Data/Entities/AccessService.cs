namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// Contains relevant details to render IIIF AccessService
/// </summary>
public class AccessService
{
    public Guid Id { get; set; }
    public int Customer { get; set; } 
    public Guid? RoleProviderId { get; set; }
    
    /// <summary>
    /// Child AccessService are services such as logout 
    /// </summary>
    public Guid? ChildAccessServiceId { get; set; }
    
    public Guid? ParentAccessServiceId { get; set; }

    public string Name { get; set; } = null!;
    public string? Profile { get; set; }
    public string? Label { get; set; }
    public string? Heading { get; set; }
    public string? Note { get; set; }
    public string? ConfirmLabel { get; set; }
    
    public RoleProvider? RoleProvider { get; set; }
    
    public List<AccessService> ChildAccessServices { get; set; }
    public AccessService? ParentAccessService { get; set; }
}