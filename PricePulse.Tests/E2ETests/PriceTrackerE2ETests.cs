using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using PricePulse.Core.Configuration;
using PricePulse.Core.Services;
using PricePulse.Core.Interfaces;
using PricePulse.Tests.IntegrationTests.TestServer;
using PricePulse.Tests.IntegrationTests.TestLogger;

namespace PricePulse.Tests.E2E
{
    public class PriceTrackerE2ETests : IClassFixture<TestServerFixture>, IAsyncLifetime
    {
        private readonly TestServerFixture _fixture;
        private readonly ITestOutputHelper _output;
        private ILogger<PriceTrackerE2ETests>? _logger;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private readonly string _screenshotsRoot;

        public PriceTrackerE2ETests(TestServerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _screenshotsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(Path.Combine(_screenshotsRoot, "Error"));
            Directory.CreateDirectory(Path.Combine(_screenshotsRoot, "Success"));
        }

        public async Task InitializeAsync()
        {
            _logger = CreateLogger();
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
            }
            
            if (_playwright != null)
            {
                _playwright.Dispose();
            }
        }

        private ILogger<PriceTrackerE2ETests> CreateLogger()
            => new TestLogger<PriceTrackerE2ETests>(_output);

        [Fact(Skip = "Run manually or in CI only - hits real resources")]
        public async Task FullApplication_RunsWithRealServices_CompletesSuccessfully()
        {
            // Arrange - Create temporary appsettings.json
            var tempConfig = Path.Combine(Path.GetTempPath(), $"appsettings_{Guid.NewGuid()}.json");
            var config = new
            {
                Logging = new { LogLevel = new { Default = "Information" } },
                WebScraping = new { UserAgent = "PricePulse-E2E-Test", SelectorTimeoutMs = 10000 },
                Tracking = new
                {
                    Products = new[]
                    {
                        new
                        {
                            Id = "e2e-test",
                            DisplayName = "E2E Test Product",
                            Url = $"{_fixture.BaseAddress}/test-page-1.html",
                            CssSelector = ".price"
                        }
                    }
                },
                Telegram = new { BotToken = "test", ChatId = "test", BaseUrl = "https://api.telegram.org", TimeoutSeconds = 30 },
                Prometheus = new { PushGatewayUrl = "http://localhost:9091" },
                Storage = new { ConnectionString = "InMemory" }
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = System.Text.Json.JsonSerializer.Serialize(config, options);

            await File.WriteAllTextAsync(tempConfig, json);

            try
            {
                // Act - Run the actual CLI application
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project ../PricePulse.Core/PricePulse.Core.csproj",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                psi.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Test";

                using var process = Process.Start(psi);
                await process.WaitForExitAsync();

                var output = await process.StandardOutput.ReadToEndAsync();
                _output.WriteLine(output);

                // Assert
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("completed successfully", output);
            }
            finally
            {
                if (File.Exists(tempConfig))
                    File.Delete(tempConfig);
            }
        }

        [Fact]
        public async Task MCP_ExtractsPrice_FromTestPage()
        {
            var stopwatch = Stopwatch.StartNew();
            string screenshotPath = null;
            var testName = nameof(MCP_ExtractsPrice_FromTestPage);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            IBrowserContext context = null;
            IPage page = null;

            try
            {
                context = await _browser.NewContextAsync();
                page = await context.NewPageAsync();

                var testUrl = $"{_fixture.BaseAddress}/test-page-1.html";
                _logger.LogInformation($"Navigating to {testUrl}");
                await page.GotoAsync(testUrl);

                var priceSelector = ".price";
                var elementHandle = await page.WaitForSelectorAsync(priceSelector);

                var priceText = await elementHandle.TextContentAsync();
                _logger.LogInformation($"Price text found: {priceText}");
                Assert.NotNull(priceText);

                if (!decimal.TryParse(
                        priceText.Replace("$", "").Replace(",", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var price))
                {
                    throw new Exception($"Failed to parse price: {priceText}");
                }

                _logger.LogInformation($"Parsed price: {price}");
                Assert.Equal(799.99m, price);

                screenshotPath = Path.Combine(_screenshotsRoot, "Success", $"{testName}_{timestamp}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                _logger.LogInformation($"MCP test passed. Screenshot saved at {screenshotPath}");
            }
            catch (Exception ex)
            {
                if (page != null)
                {
                    screenshotPath = Path.Combine(_screenshotsRoot, "Error", $"{testName}_{timestamp}.png");
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                    _logger.LogError(ex, $"MCP test failed. Screenshot saved at {screenshotPath}");
                }
                else
                {
                    _logger.LogError(ex, "MCP test failed before page loaded");
                }

                throw;
            }
            finally
            {
                if (context != null)
                {
                    await context.CloseAsync();
                    _logger.LogInformation("Browser context closed after test");
                }
                stopwatch.Stop();
                _logger.LogInformation($"MCP test duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            }
        }

        [Fact]
        public async Task ExtractsPrice_FromDynamicPage_WaitsForJavaScript()
        {
            var stopwatch = Stopwatch.StartNew();
            string screenshotPath = null;
            var testName = nameof(ExtractsPrice_FromDynamicPage_WaitsForJavaScript);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            IBrowserContext context = null;
            IPage page = null;

            try
            {
                context = await _browser.NewContextAsync();
                page = await context.NewPageAsync();

                var testUrl = $"{_fixture.BaseAddress}/dynamic-price-page.html";
                _logger.LogInformation($"Navigating to dynamic page: {testUrl}");
                await page.GotoAsync(testUrl);

                // Wait for the dynamic price to load (JavaScript updates after 1 second)
                var priceSelector = "#dynamic-price";
                var elementHandle = await page.WaitForSelectorAsync(priceSelector, new() { Timeout = 5000 });
                
                // Wait for element to be stable
                await elementHandle.WaitForElementStateAsync(ElementState.Stable, new() { Timeout = 2000 });
                var priceText = await elementHandle.TextContentAsync();
                
                // If still loading, wait a bit more
                if (priceText.Contains("Loading"))
                {
                    await Task.Delay(1500);
                    priceText = await elementHandle.TextContentAsync();
                }

                _logger.LogInformation($"Dynamic price text found: {priceText}");

                if (!decimal.TryParse(
                        priceText.Replace("$", "").Replace(",", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var price))
                {
                    throw new Exception($"Failed to parse dynamic price: {priceText}");
                }

                _logger.LogInformation($"Parsed dynamic price: {price}");
                Assert.Equal(599.00m, price);

                screenshotPath = Path.Combine(_screenshotsRoot, "Success", $"{testName}_{timestamp}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                _logger.LogInformation($"Dynamic price test passed. Screenshot saved at {screenshotPath}");
            }
            catch (Exception ex)
            {
                if (page != null)
                {
                    screenshotPath = Path.Combine(_screenshotsRoot, "Error", $"{testName}_{timestamp}.png");
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                    _logger.LogError(ex, $"Dynamic price test failed. Screenshot saved at {screenshotPath}");
                }
                else
                {
                    _logger.LogError(ex, "Dynamic price test failed before page loaded");
                }

                throw;
            }
            finally
            {
                if (context != null)
                {
                    await context.CloseAsync();
                    _logger.LogInformation("Browser context closed after dynamic test");
                }
                stopwatch.Stop();
                _logger.LogInformation($"Dynamic price test duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            }
        }

        [Fact]
        public async Task ExtractsPrice_FromSalePage_DetectsPriceDrop()
        {
            var stopwatch = Stopwatch.StartNew();
            string screenshotPath = null;
            var testName = nameof(ExtractsPrice_FromSalePage_DetectsPriceDrop);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            IBrowserContext context = null;
            IPage page = null;

            try
            {
                context = await _browser.NewContextAsync();
                page = await context.NewPageAsync();

                var testUrl = $"{_fixture.BaseAddress}/sale-item.html";
                _logger.LogInformation($"Navigating to sale page: {testUrl}");
                await page.GotoAsync(testUrl);

                // Wait for sale price to load (JavaScript updates after 1.5 seconds)
                var priceSelector = "#sale-price";
                var elementHandle = await page.WaitForSelectorAsync(priceSelector, new() { Timeout = 5000 });
                
                // Wait for element to be stable
                await elementHandle.WaitForElementStateAsync(ElementState.Stable, new() { Timeout = 2000 });
                var priceText = await elementHandle.TextContentAsync();
                
                // If still loading, wait a bit more
                if (priceText.Contains("Loading"))
                {
                    await Task.Delay(2000);
                    priceText = await elementHandle.TextContentAsync();
                }

                _logger.LogInformation($"Sale price text found: {priceText}");

                if (!decimal.TryParse(
                        priceText.Replace("$", "").Replace(",", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var price))
                {
                    throw new Exception($"Failed to parse sale price: {priceText}");
                }

                _logger.LogInformation($"Parsed sale price: {price}");
                Assert.Equal(399.99m, price);

                screenshotPath = Path.Combine(_screenshotsRoot, "Success", $"{testName}_{timestamp}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                _logger.LogInformation($"Sale price test passed. Screenshot saved at {screenshotPath}");
            }
            catch (Exception ex)
            {
                if (page != null)
                {
                    screenshotPath = Path.Combine(_screenshotsRoot, "Error", $"{testName}_{timestamp}.png");
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                    _logger.LogError(ex, $"Sale price test failed. Screenshot saved at {screenshotPath}");
                }
                else
                {
                    _logger.LogError(ex, "Sale price test failed before page loaded");
                }

                throw;
            }
            finally
            {
                if (context != null)
                {
                    await context.CloseAsync();
                    _logger.LogInformation("Browser context closed after sale test");
                }
                stopwatch.Stop();
                _logger.LogInformation($"Sale price test duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            }
        }
    }
}