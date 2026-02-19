using PricePulse.Core.Configuration;

namespace PricePulse.Core.Interfaces;

/// <summary>
/// Defines a contract for retrieving a product price from a given source.
/// </summary>
public interface IPriceProvider
{
    /// <summary>
    /// Asynchronously retrieves the product price using the provided configuration.
    /// </summary>
    Task<decimal> GetPriceAsync(ProductConfig product);
}