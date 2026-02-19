using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PricePulse.Core.Configuration;
using PricePulse.Core.Services;
using Moq;
using Xunit;

namespace PricePulse.Tests;

public class WebPriceExtractorTests
{
    private readonly Mock<ILogger<WebPriceExtractor>> _mockLogger;
    private readonly WebScrapingOptions _options;

    public WebPriceExtractorTests()
    {
        _mockLogger = new Mock<ILogger<WebPriceExtractor>>();
        _options = new WebScrapingOptions
        {
            SelectorTimeoutMs = 10000,
            UserAgent = "Test User Agent"
        };
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsNull()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => extractor.GetPriceAsync(null!));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsEmpty()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        var product = new ProductConfig
        {
            Id = "test",
            DisplayName = "Test",
            Url = string.Empty,
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(product));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsWhitespace()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        var product = new ProductConfig
        {
            Id = "test",
            DisplayName = "Test",
            Url = "   ",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(product));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsInvalid()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        var product = new ProductConfig
        {
            Id = "test",
            DisplayName = "Test",
            Url = "not-a-valid-url",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(product));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsFileProtocol()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        var product = new ProductConfig
        {
            Id = "test",
            DisplayName = "Test",
            Url = "file:///path/to/file",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(product));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsJavaScriptProtocol()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        var product = new ProductConfig
        {
            Id = "test",
            DisplayName = "Test",
            Url = "javascript:alert('xss')",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(product));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldAcceptValidHttpUrl()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);
        var product = new ProductConfig
        {
            Id = "example-http",
            DisplayName = "Example (HTTP)",
            Url = "http://example.com",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        // Act
        // This will fail to extract price (returns 0) but shouldn't throw ArgumentException
        decimal result;
        try
        {
            result = await extractor.GetPriceAsync(product);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Assert
        Assert.True(result >= 0, "Should return non-negative value even if price not found");
    }

    [Fact]
    public async Task GetPriceAsync_ShouldAcceptValidHttpsUrl()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);
        var product = new ProductConfig
        {
            Id = "example-https",
            DisplayName = "Example (HTTPS)",
            Url = "https://example.com",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        // Act
        // This will fail to extract price (returns 0) but shouldn't throw ArgumentException
        decimal result;
        try
        {
            result = await extractor.GetPriceAsync(product);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Assert
        Assert.True(result >= 0, "Should return non-negative value even if price not found");
    }
}
