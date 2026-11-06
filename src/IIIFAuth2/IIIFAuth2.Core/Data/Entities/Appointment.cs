using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.Core.Data.Entities;

/// <summary>
/// An appointment grants <see cref="Roles"/> for assets matching <see cref="AssetPatterns"/>, between
/// <see cref="Start"/> and <see cref="End"/>, to whoever enters <see cref="Token"/>
/// </summary>
public class Appointment : IHaveCustomer
{
    public Guid Id { get; set; }
    public int Customer { get; set; }

    /// <summary>
    /// The Appointment role-provider this appointment belongs to
    /// </summary>
    public Guid RoleProviderId { get; set; }
    public RoleProvider RoleProvider { get; set; } = null!;

    /// <summary>
    /// The appointment key the user enters, unique per customer
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Roles granted by this appointment, for matching assets
    /// </summary>
    public List<string> Roles { get; set; } = new();

    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>
    /// Whether appointment has been approved and can be used
    /// </summary>
    public bool Active { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? DateApproved { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public DateTime Created { get; set; }

    /// <summary>
    /// Patterns that assets must match for <see cref="Roles"/> to be granted
    /// </summary>
    public AssetPatterns AssetPatterns { get; set; } = new();
}
