namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// SessionUser manages AccessToken + Cookies and associated roles this user has.
/// </summary>
/// <remarks>All roles expire and extend in lock-step.</remarks>
public class SessionUser : IHaveCustomer
{
    public Guid Id { get; set; }
    public int Customer { get; set; }
    
    /// <summary>
    /// This value is set in the cookie (authorizing aspect)
    /// </summary>
    public string CookieId { get; set; } = null!;
    
    /// <summary>
    /// The origin that was sent when creating cookie
    /// </summary>
    public string Origin { get; set; } = null!;
    
    /// <summary>
    /// Token returned from AccessTokenService, used in requests to ProbeService
    /// </summary>
    public string AccessToken { get; set; } = null!;
    
    public DateTime Expires { get; set; }
    
    public DateTime Created { get; set; }
    
    /// <summary>
    /// When expiry last checked
    /// </summary>
    public DateTime? LastChecked { get; set; }
    
    /// <summary>
    /// List of roles this session has access to
    /// </summary>
    public List<string> Roles { get; set; } = new();
}