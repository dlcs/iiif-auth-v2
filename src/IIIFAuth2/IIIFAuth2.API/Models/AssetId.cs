namespace IIIFAuth2.API.Models;

/// <summary>
/// A record that represents an identifier for a DLCS Asset.
/// </summary>
public record AssetId(int Customer, int Space, string Asset)
{
    public override string ToString() => $"{Customer}/{Space}/{Asset}";
    
    public static AssetId FromString(string assetImageId)
    {
        var parts = assetImageId.Split("/", StringSplitOptions.RemoveEmptyEntries);
        
        try
        {
            return new AssetId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
        catch (FormatException fmEx)
        {
            throw new FormatException(
                $"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset",
                fmEx);
        }
        catch (IndexOutOfRangeException idxEx)
        {
            throw new FormatException(
                $"AssetId '{assetImageId}' is invalid. Must be in format customer/space/asset",
                idxEx);
        }
    }
}
