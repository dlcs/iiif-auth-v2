namespace IIIFAuth2.API.Data.Entities;

/// <summary>
/// Marker interface for objects that are customer specific
/// </summary>
public interface IHaveCustomer
{
    public int Customer { get; }
}