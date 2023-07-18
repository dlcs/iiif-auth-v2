namespace IIIFAuth2.API.Data;

public static class CacheKeys
{
    public static string Customer(int customerId) => $"c:{customerId}";

    public static readonly string AccessService = "accessServices";

    public static readonly string Roles = "roles";
}