namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// Records single user correlation token for role provisioning.
/// Used when asking user to carry out a 'significant gesture' to allow us to issue a cookie or as a temporary nonce
/// value when posting to identify providers.
/// </summary>
public class RoleProvisionToken : IHaveCustomer
{
    /// <summary>
    /// The generated single-use token
    /// </summary>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// Marker of whether this token has been posted back already
    /// </summary>
    public bool Used { get; set; }
    
    /// <summary>
    /// The list of roles that this token is for, if known
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
    public DateTime Created { get; set; }
    
    public int Customer { get; set; }

    public string Origin { get; set; } = null!;
    
    /// <summary>
    /// For optimistic-concurrency, see https://www.npgsql.org/efcore/modeling/concurrency.html
    /// </summary>
    public uint Version { get; set; }
}