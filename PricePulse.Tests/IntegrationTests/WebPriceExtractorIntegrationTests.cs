using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PricePulse.Core.Configuration;
using PricePulse.Core.Services;
using Xunit;
using Xunit.Abstractions;
using PricePulse.Core.Interfaces;
using PricePulse.Tests.IntegrationTests.TestServer;
using PricePulse.Tests.IntegrationTests.TestLogger;

namespace PricePulse.Tests.Integration;

public class WebPriceExtractorIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TestServer _server;
    private IPlaywright _playwright;
    private IBrowser _browser;

    public WebPriceExtractorIntegrationTests(ITestOutputHelper output)
        => _output = output;

    public async Task InitializeAsync()
    {
        // Запуск локального серверу
        _server = new TestServer();

        // Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
        await _server.DisposeAsync();
    }

    // --- Factory methods to avoid duplication (DRY) ---
    private IOptions<WebScrapingOptions> CreateOptions(int timeoutMs = 5000)
        => Options.Create(new WebScrapingOptions
        {
            UserAgent = "PricePulse-TestBot/1.0",
            SelectorTimeoutMs = timeoutMs
        });

    private ILogger<WebPriceExtractor> CreateLogger()
        => new TestLogger<WebPriceExtractor>(_output);

    private WebPriceExtractor CreateExtractor(IOptions<WebScrapingOptions>? options = null)
        => new WebPriceExtractor(options ?? CreateOptions(), CreateLogger());

    
    [Fact]
    public async Task GetPriceAsync_ExtractsPrice_FromLocalTestPage()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync($"{_server.Url}test-page-1.html");

        var priceText = await page.TextContentAsync(".price");
        Assert.Equal("$799.99", priceText?.Trim());
    }

    [Fact]
    public async Task GetPriceAsync_ExtractsSalePrice_FromSaleItemPage()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync($"{_server.Url}sale-item.html");

        // Wait for the element to be present
        var element = await page.WaitForSelectorAsync("#sale-price", new() { Timeout = 5000 });
        
        // Wait for element to be stable (JavaScript will update it)
        await element.WaitForElementStateAsync(ElementState.Stable, new() { Timeout = 3000 });
        
        // Get the text content after it's updated
        var priceText = await element.TextContentAsync();
        
        // If still loading, wait a bit more for JavaScript to execute
        if (priceText?.Contains("Loading") == true)
        {
            await Task.Delay(2000);
            priceText = await element.TextContentAsync();
        }
        
        Assert.Equal("$399.99", priceText?.Trim());
    }

    [Fact]
    public async Task GetPriceAsync_HandlesDynamicContent_WaitForSelector()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync($"{_server.Url}dynamic-price-page.html");

        var element = await page.WaitForSelectorAsync("#dynamic-price");
        var priceText = await element.TextContentAsync();
        Assert.True(!string.IsNullOrEmpty(priceText));
    }
}