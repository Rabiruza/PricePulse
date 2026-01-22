using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PricePulse.Core;

public class PlaywrightScraper : IScraperService
{
    public async Task<decimal> GetPriceAsync(string url)
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Apple іноді перевіряє User-Agent, додамо стандартний заголовок
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string> 
        {
            { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
        });

        await page.GotoAsync(url);

        try 
        {
            // 1. Чекаємо на конкретний селектор, який ти знайшла
            string selector = "span[data-pricing-product='iphone-17']";
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 10000 });

            // 2. Отримуємо текст (наприклад, "From $799")
            var priceElement = await page.QuerySelectorAsync(selector);
            if (priceElement != null)
            {
                var priceText = await priceElement.InnerTextAsync();
                Console.WriteLine($"[Scraper] Raw price found: {priceText}");

                // 3. Очищаємо текст від "From $", залишаючи тільки цифри
                // Використовуємо Regex, щоб знайти перше число
                var match = Regex.Match(priceText, @"\d+");
                if (match.Success)
                {
                    return decimal.Parse(match.Value, CultureInfo.InvariantCulture);
                }
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[Scraper] Timeout: Price element not found. Apple might be changing the layout or blocking the request.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scraper] Error: {ex.Message}");
        }

        return 0m;
    }
}