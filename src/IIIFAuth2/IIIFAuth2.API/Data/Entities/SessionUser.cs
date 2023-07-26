namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// SessionUser manages AccessToken + Cookies and associated roles this user has.
/// </summary>
/// <remarks>All roles expire and extend in lock-step.</remarks>
public class SessionUser : IHaveCustomer
{
    public Guid Id { get; set; }
    public int Customer { get; set; }
    public string CookieId { get; set; } = null!;
    public string AccessToken { get; set; } = null!;
    public DateTime Expires { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastChecked { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}