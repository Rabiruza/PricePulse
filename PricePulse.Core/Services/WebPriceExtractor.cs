using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class WebPriceExtractor : IPriceProvider
{
    private readonly WebScrapingOptions _options;
    private readonly ILogger<WebPriceExtractor> _logger;

    public WebPriceExtractor(IOptions<WebScrapingOptions> options, ILogger<WebPriceExtractor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetPriceAsync(ProductConfig product)
    {
        if (product is null)
        {
            _logger.LogError("Product configuration cannot be null");
            throw new ArgumentNullException(nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.Url))
        {
            _logger.LogError("Product {ProductId} URL cannot be null or empty", product.Id);
            throw new ArgumentException("Product URL cannot be null or empty", nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.CssSelector))
        {
            _logger.LogError("Product {ProductId} CSS selector cannot be null or empty", product.Id);
            throw new ArgumentException("Product CSS selector cannot be null or empty", nameof(product));
        }

        // Validate URL format for security
        if (!Uri.TryCreate(product.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("Invalid URL format: {Url}. Only HTTP/HTTPS URLs are allowed.", product.Url);
            throw new ArgumentException("URL must be a valid HTTP or HTTPS URL", nameof(product));
        }

        _logger.LogInformation(
            "Starting price extraction for {ProductId} from {Url}",
            product.Id,
            product.Url);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string> 
        {
            { "User-Agent", _options.UserAgent }
        });

        try
        {
            await page.GotoAsync(product.Url);
            _logger.LogDebug("Successfully navigated to {Url}", product.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Url}", product.Url);
            return 0m;
        }

        try 
        {
            string selector = product.CssSelector;
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = _options.SelectorTimeoutMs });

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
                        _logger.LogInformation("Successfully extracted price: ${Price} from {Url}", price, product.Url);
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
            _logger.LogWarning(
                "Timeout waiting for price element. Selector: {Selector}. Site might be changing layout or blocking the request.",
                product.CssSelector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting price from {Url}", product.Url);
        }

        _logger.LogWarning("Failed to extract price from {Url}, returning 0", product.Url);
        return 0m;
    }
}