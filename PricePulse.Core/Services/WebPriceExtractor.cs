using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class WebPriceExtractor : IPriceProvider
{
    private const int SelectorTimeoutMs = 10000;
    private readonly ILogger<WebPriceExtractor> _logger;

    public WebPriceExtractor(ILogger<WebPriceExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetPriceAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogError("URL cannot be null or empty");
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        // Validate URL format for security
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("Invalid URL format: {Url}. Only HTTP/HTTPS URLs are allowed.", url);
            throw new ArgumentException("URL must be a valid HTTP or HTTPS URL", nameof(url));
        }

        _logger.LogInformation("Starting price extraction from {Url}", url);

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string> 
        {
            { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
        });

        try
        {
            await page.GotoAsync(url);
            _logger.LogDebug("Successfully navigated to {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Url}", url);
            return 0m;
        }

        try 
        {
            string selector = "span[data-pricing-product='iphone-17']";
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = SelectorTimeoutMs });

            var priceElement = await page.QuerySelectorAsync(selector);
            if (priceElement != null)
            {
                var priceText = await priceElement.InnerTextAsync();
                _logger.LogDebug("Raw price text found: {PriceText}", priceText);
                
                // Improved regex to handle: "From $799", "$799.99", "799", "799.99", etc.
                // Matches: optional currency symbols, optional "From" prefix, then digits with optional decimal part
                var priceMatch = Regex.Match(priceText, @"(?:\$|USD|€|£)?\s*(?:From\s+)?(\d+(?:\.\d{1,2})?)", 
                    RegexOptions.IgnoreCase);
                
                if (priceMatch.Success && priceMatch.Groups.Count > 1)
                {
                    var priceValue = priceMatch.Groups[1].Value;
                    if (decimal.TryParse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                    {
                        _logger.LogInformation("Successfully extracted price: ${Price} from {Url}", price, url);
                        return price;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse extracted price value: {PriceValue} from text: {PriceText}", 
                            priceValue, priceText);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract numeric price from text: {PriceText}", priceText);
                }
            }
            else
            {
                _logger.LogWarning("Price element not found with selector: {Selector}", selector);
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for price element. Selector: {Selector}. Apple might be changing the layout or blocking the request.", 
                "span[data-pricing-product='iphone-17']");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting price from {Url}", url);
        }

        _logger.LogWarning("Failed to extract price from {Url}, returning 0", url);
        return 0m;
    }
}