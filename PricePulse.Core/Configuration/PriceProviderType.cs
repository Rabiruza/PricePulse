namespace PricePulse.Core.Configuration;

/// <summary>
/// Defines the type of price provider strategy to use for extracting prices from different sources.
/// </summary>
public enum PriceProviderType
{
    /// <summary>
    /// Generic Playwright-based provider that uses CSS selectors. Suitable for most websites.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Apple-specific provider that handles Apple's DOM structure and pricing logic.
    /// </summary>
    Apple = 1,

    /// <summary>
    /// Amazon-specific provider for Amazon product pages.
    /// </summary>
    Amazon = 2,

    /// <summary>
    /// REST API-based provider for sites that offer public or hidden APIs instead of HTML scraping.
    /// </summary>
    RestApi = 3
}