using IIIF.Presentation.V3.Strings;

namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// Contains relevant details to render IIIF AccessService
/// </summary>
public class AccessService : IHaveCustomer
{
    public Guid Id { get; set; }
    public int Customer { get; set; } 
    public Guid? RoleProviderId { get; set; }

    public string Name { get; set; } = null!;
    
    /// <summary>
    /// The profile of the service - active/kiosk/external
    /// </summary>
    public string Profile { get; set; } = null!;
    
    /// <summary>
    /// The name of the access service.
    /// </summary>
    public LanguageMap? Label { get; set; }
    
    /// <summary>
    /// Heading text to be shown with the user interface element that opens the access service.
    /// </summary>
    public LanguageMap? Heading { get; set; }
    
    /// <summary>
    /// Additional text to be shown with the user interface element that opens the access service. 
    /// </summary>
    public LanguageMap? Note { get; set; }
    
    /// <summary>
    /// The label for the user interface element that opens the access service.
    /// </summary>
    public LanguageMap? ConfirmLabel { get; set; }
    
    /// <summary>
    /// The name of the logout service. Optional, default used if not present.
    /// </summary>
    public LanguageMap? LogoutLabel { get; set; }
    
    /// <summary>
    /// Default heading text to render if an error occurs on AccessToken service
    /// </summary>
    public LanguageMap? AccessTokenErrorHeading { get; set; }
    
    /// <summary>
    /// Default heading text to render if an error occurs on AccessToken service
    /// </summary>
    public LanguageMap? AccessTokenErrorNote { get; set; }
    
    public RoleProvider? RoleProvider { get; set; }
}