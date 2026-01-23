namespace PricePulse.Core.Interfaces;

/// <summary>
/// Provides an abstraction for storing and retrieving product prices.
/// Decouples consumers from the storage implementation (e.g., database, memory, file).
/// </summary>
public interface IPriceStorage
{
    /// <summary>
    /// Gets the most recently stored price.
    /// </summary>
    Task<decimal> GetLastPriceAsync();

    /// <summary>
    /// Stores the specified price.
    /// </summary>
    /// <param name="price">Price to save.</param>
    Task SavePriceAsync(decimal price);
}