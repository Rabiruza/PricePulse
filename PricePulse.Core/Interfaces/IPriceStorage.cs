using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Interfaces;

/// <summary> Stores and retrieves product prices. </summary>
public interface IPriceStorage
{
    /// <summary> Gets the most recently stored price. </summary>
    Task<decimal> GetLastPriceAsync();

    /// <summary>
    /// Stores the specified price.
    /// </summary>
    /// <param name="price">Price to save.</param>
    Task SavePriceAsync(decimal price);
}