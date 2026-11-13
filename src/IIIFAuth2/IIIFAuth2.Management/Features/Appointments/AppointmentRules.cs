using System.Security.Cryptography;
using IIIFAuth2.Core.Data;
using IIIFAuth2.Core.Data.Entities;
using IIIFAuth2.Core.Models.Domain;
using IIIFAuth2.Management.Features.Appointments.Models;
using IIIFAuth2.Management.Utils;
using Microsoft.EntityFrameworkCore;

namespace IIIFAuth2.Management.Features.Appointments;

/// <summary>
/// Token generation and validation shared by appointment create/update requests
/// </summary>
public class AppointmentRules(AuthServicesContext dbContext)
{
    private const string TokenChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    public const int TokenLength = 12;

    public static string GenerateToken() => RandomNumberGenerator.GetString(TokenChars, TokenLength);

    /// <summary>
    /// Apply <paramref name="request"/> to <paramref name="appointment"/> and validate the result, setting the
    /// role-provider from the roles. If <paramref name="replace"/> (POST/PUT) omitted optional fields are cleared,
    /// otherwise (PATCH) omitted fields are unchanged. Required fields are enforced by <see cref="AppointmentRequest"/>
    /// model validation
    /// </summary>
    /// <returns>Validation error message, or null if valid</returns>
    public async Task<string?> ApplyAndValidate(Appointment appointment, IAppointmentFields request, bool replace,
        CancellationToken cancellationToken)
    {
        // Checked before Apply, which shares request.AssetPatterns with the entity and overwrites customer
        if (request.AssetPatterns?.Customer?.Any(c => c != appointment.Customer) == true)
            return $"assetPatterns.customer can only be {appointment.Customer}";

        Apply(appointment, request, replace);

        var errors = Validate(appointment, request);
        if (errors.Count > 0) return string.Join("; ", errors);

        var (roleProviderId, roleError) = await ResolveRoleProvider(appointment, cancellationToken);
        if (roleError != null) return roleError;

        appointment.RoleProviderId = roleProviderId!.Value;
        if (appointment is { Active: true, DateApproved: null })
        {
            // Truncate to Postgres microsecond precision, so returned value matches what is later read
            var now = DateTime.UtcNow;
            appointment.DateApproved = now.AddTicks(-(now.Ticks % 10));
        }
        else if (!appointment.Active)
        {
            // Cleared so re-approving sets a fresh date
            appointment.DateApproved = null;
        }

        return null;
    }

    private static void Apply(Appointment appointment, IAppointmentFields request, bool replace)
    {
        if (request.Name != null) appointment.Name = request.Name;
        if (request.Email != null) appointment.Email = request.Email;
        if (replace || request.Notes != null) appointment.Notes = request.Notes;
        if (request.Start.HasValue) appointment.Start = request.Start.Value.ToUtc();
        if (request.End.HasValue) appointment.End = request.End.Value.ToUtc();
        if (request.Active.HasValue) appointment.Active = request.Active.Value;
        if (replace || request.ApprovedBy != null) appointment.ApprovedBy = request.ApprovedBy;
        if (request.Roles != null) appointment.Roles = request.Roles.Distinct().ToList();
        if (request.AssetPatterns != null) appointment.AssetPatterns = request.AssetPatterns;

        // Appointments only ever apply to the owning customer, a different supplied value fails validation
        appointment.AssetPatterns.Customer = [appointment.Customer];
    }

    private static List<string> Validate(Appointment appointment, IAppointmentFields request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(appointment.Name)) errors.Add("name cannot be empty");
        if (string.IsNullOrWhiteSpace(appointment.Email)) errors.Add("email cannot be empty");
        if (appointment.Roles.Count == 0) errors.Add("roles cannot be empty");
        if (appointment.End <= appointment.Start) errors.Add("end must be after start");

        // Checked against request, not entity, so a PATCH can deactivate without clearing who approved it
        if (request.ApprovedBy != null && !appointment.Active) errors.Add("approvedBy can only be set if active");
        if (appointment.Active && string.IsNullOrWhiteSpace(appointment.ApprovedBy))
            errors.Add("approvedBy is required if active");
        return errors;
    }

    /// <summary>
    /// All roles must exist for customer and belong to access-services of a single Appointment role-provider
    /// </summary>
    private async Task<(Guid? RoleProviderId, string? Error)> ResolveRoleProvider(Appointment appointment,
        CancellationToken cancellationToken)
    {
        // TODO - once we have RoleProvider/Role management we'll likely have helpers for some of this
        var roles = await dbContext.Roles.AsNoTracking()
            .Where(r => r.Customer == appointment.Customer && appointment.Roles.Contains(r.Id))
            .Join(dbContext.AccessServices, r => r.AccessServiceId, a => a.Id,
                (r, a) => new { r.Id, a.RoleProviderId })
            .ToListAsync(cancellationToken);

        var unknown = appointment.Roles.Except(roles.Select(r => r.Id)).ToList();
        if (unknown.Count > 0) return (null, $"Unknown roles: {string.Join(", ", unknown)}");

        var providerIds = roles.Select(r => r.RoleProviderId).Distinct().ToList();
        if (providerIds is not [{ } providerId])
            return (null, "roles must all belong to the same Appointment role-provider");

        var provider = await dbContext.RoleProviders.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == providerId && p.Customer == appointment.Customer, cancellationToken);
        return provider?.Configuration.GetConfigurationOrDefault(RoleProviderConfiguration.DefaultKey)
            is AppointmentConfiguration
            ? (providerId, null)
            : (null, "roles must belong to an Appointment role-provider");
    }
}
