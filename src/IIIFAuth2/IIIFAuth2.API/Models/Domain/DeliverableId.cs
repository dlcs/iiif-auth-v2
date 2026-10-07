namespace IIIFAuth2.API.Models.Domain;

/// <summary>
/// A record that represents an identifier for a DLCS deliverable - either an Asset or an Adjunct of an Asset.
/// </summary>
public record DeliverableId(int Customer, int Space, string Asset, string? AdjunctId = null)
{
    public override string ToString() =>
        AdjunctId == null ? $"{Customer}/{Space}/{Asset}" : $"{Customer}/{Space}/{Asset}/{AdjunctId}";

    public static DeliverableId FromString(string deliverableId)
    {
        var parts = deliverableId.Split("/", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 3 or > 4) throw InvalidFormat(deliverableId);

        try
        {
            return new DeliverableId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2],
                parts.Length == 4 ? parts[3] : null);
        }
        catch (FormatException fmEx)
        {
            throw InvalidFormat(deliverableId, fmEx);
        }
    }

    private static FormatException InvalidFormat(string deliverableId, Exception? inner = null)
        => new(
            $"DeliverableId '{deliverableId}' is invalid. Must be in format customer/space/asset or customer/space/asset/adjunct",
            inner);
}
