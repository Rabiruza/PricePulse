using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(null!));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsEmpty()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync(string.Empty));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsWhitespace()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync("   "));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsInvalid()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync("not-a-valid-url"));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsFileProtocol()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync("file:///path/to/file"));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldThrowArgumentException_WhenUrlIsJavaScriptProtocol()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => extractor.GetPriceAsync("javascript:alert('xss')"));
    }

    [Fact]
    public async Task GetPriceAsync_ShouldAcceptValidHttpUrl()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);
        var url = "http://example.com";

        // Act
        // This will fail to extract price (returns 0) but shouldn't throw ArgumentException
        var result = await extractor.GetPriceAsync(url);

        // Assert
        Assert.True(result >= 0, "Should return non-negative value even if price not found");
    }

    [Fact]
    public async Task GetPriceAsync_ShouldAcceptValidHttpsUrl()
    {
        // Arrange
        var options = Options.Create(_options);
        var extractor = new WebPriceExtractor(options, _mockLogger.Object);
        var url = "https://example.com";

        // Act
        // This will fail to extract price (returns 0) but shouldn't throw ArgumentException
        var result = await extractor.GetPriceAsync(url);

        // Assert
        Assert.True(result >= 0, "Should return non-negative value even if price not found");
    }
}
