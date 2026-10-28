using System.Text.Json.Serialization;

namespace IIIFAuth2.Management.Features.CustomerCreation;

/// <summary>
/// Notification raised by DLCS when a new customer is created
/// </summary>
/// <remarks>Id is required, without it deserialization would default to 0</remarks>
public record CustomerCreatedMessage([property: JsonRequired] int Id, string Name);
