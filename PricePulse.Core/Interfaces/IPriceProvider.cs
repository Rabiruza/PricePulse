namespace PricePulse.Core.Interfaces;

/// <summary>
/// Defines a contract for retrieving a product price from a given source.
/// </summary>
public interface IPriceProvider
{
    /// <summary>
    /// Asynchronously retrieves the product price for the specified URL.
    /// </summary>
    Task<decimal> GetPriceAsync(string url);
}