using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PricePulse.Core;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;
using PricePulse.Core.Services;
using Moq;
using Xunit;

namespace PricePulse.Tests;

public class PriceTrackerTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPriceAsync_ShouldReturnSuccess_WhenUrlIsValid()
    {
        // Arrange 
        var mockLogger = new Mock<ILogger<WebPriceExtractor>>();
        var options = Microsoft.Extensions.Options.Options.Create(new PricePulse.Core.Configuration.WebScrapingOptions
        {
            SelectorTimeoutMs = 10000,
            UserAgent = "Test User Agent"
        });
        var priceProvider = new WebPriceExtractor(options, mockLogger.Object);
        var product = new ProductConfig
        {
            Id = "microsoft",
            DisplayName = "Microsoft",
            Url = "https://www.microsoft.com",
            CssSelector = "body",
            ProviderType = PriceProviderType.Generic
        };

        // Act 
        // This is an integration test that requires Playwright browsers to be installed
        // It verifies that WebPriceExtractor doesn't crash and returns a non-negative value
        decimal result;
        try
        {
            result = await priceProvider.GetPriceAsync(product);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Assert
        Assert.True(result >= 0, "Price should be a non-negative number (0 if price not found)");
    }

    [Fact]
    public async Task RunAsync_ShouldSendNotification_WhenPriceDrops()
    {
        // --- Arrange ---
        // Create mocks for all dependencies to isolate the business logic of PriceTracker
        var mockProvider = new Mock<IPriceProvider>();
        var mockStorage = new Mock<IPriceStorage>();
        var mockNotifier = new Mock<INotificationService>();
        var mockMonitoring = new Mock<IMonitoringService>();
        var mockLogger = new Mock<ILogger<PriceTracker>>();

        // Setup: Simulate that the current price on the website is lower than the stored price
        decimal currentPrice = 800m;
        decimal lastPrice = 1000m;

        // Mocking the provider to return our "current" price
        mockProvider.Setup(p => p.GetPriceAsync(It.IsAny<ProductConfig>()))
                    .ReturnsAsync(currentPrice);

        // Mocking the storage to return our "historical" price
        mockStorage.Setup(s => s.GetLastPriceAsync())
                   .ReturnsAsync(lastPrice);

        // Initialize the tracker with mocked dependencies (Dependency Injection)
        var tracker = new PriceTracker(
            mockProvider.Object, 
            mockStorage.Object, 
            mockNotifier.Object, 
            mockMonitoring.Object,
            mockLogger.Object);

        // --- Act ---
        // Execute the tracking logic
        var product = new ProductConfig
        {
            Id = "iphone-17",
            DisplayName = "iPhone 17",
            Url = "http://example.com/iphone",
            CssSelector = "span.price",
            ProviderType = PriceProviderType.Generic
        };

        await tracker.RunAsync(product);

        // --- Assert ---
        // Verify that the notification was sent exactly once with the "SALE" message
        mockNotifier.Verify(n => n.SendAsync(It.Is<string>(s => s.Contains("📉 SALE!"))), 
                           Times.Once, 
                           "Notification should be sent when the price decreases.");
        
        // Verify that the monitoring service received the new price update
        mockMonitoring.Verify(m => m.PushMetricAsync("iPhone 17", currentPrice), 
                             Times.Once);

        // Verify that the new (lower) price was saved to the storage
        mockStorage.Verify(s => s.SavePriceAsync(currentPrice), 
                          Times.Once, 
                          "The new price must be saved regardless of the change.");
    }

    [Fact]
    public async Task RunAsync_ShouldNotSendNotification_WhenPriceDoesNotDrop()
    {
        // --- Arrange ---
        var mockProvider = new Mock<IPriceProvider>();
        var mockStorage = new Mock<IPriceStorage>();
        var mockNotifier = new Mock<INotificationService>();
        var mockMonitoring = new Mock<IMonitoringService>();
        var mockLogger = new Mock<ILogger<PriceTracker>>();

        decimal currentPrice = 1200m;
        decimal lastPrice = 1000m;

        mockProvider.Setup(p => p.GetPriceAsync(It.IsAny<ProductConfig>()))
                    .ReturnsAsync(currentPrice);

        mockStorage.Setup(s => s.GetLastPriceAsync())
                   .ReturnsAsync(lastPrice);

        var tracker = new PriceTracker(
            mockProvider.Object,
            mockStorage.Object,
            mockNotifier.Object,
            mockMonitoring.Object,
            mockLogger.Object);

        // --- Act ---
        var product = new ProductConfig
        {
            Id = "iphone-17",
            DisplayName = "iPhone 17",
            Url = "http://example.com/iphone",
            CssSelector = "span.price",
            ProviderType = PriceProviderType.Generic
        };

        await tracker.RunAsync(product);

        // --- Assert ---
        mockNotifier.Verify(n => n.SendAsync(It.IsAny<string>()),
                           Times.Never,
                           "Notification should not be sent when the price does not decrease.");

        mockMonitoring.Verify(m => m.PushMetricAsync("iPhone 17", currentPrice),
                             Times.Once);

        mockStorage.Verify(s => s.SavePriceAsync(currentPrice),
                          Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldThrowArgumentException_WhenUrlIsNullOrEmpty()
    {
        // Arrange
        var mockProvider = new Mock<IPriceProvider>();
        var mockStorage = new Mock<IPriceStorage>();
        var mockNotifier = new Mock<INotificationService>();
        var mockMonitoring = new Mock<IMonitoringService>();
        var mockLogger = new Mock<ILogger<PriceTracker>>();

        var tracker = new PriceTracker(
            mockProvider.Object,
            mockStorage.Object,
            mockNotifier.Object,
            mockMonitoring.Object,
            mockLogger.Object);

        // Act & Assert
        var missingUrl = new ProductConfig
        {
            Id = "iphone-17",
            DisplayName = "iPhone 17",
            Url = "",
            CssSelector = "span.price",
            ProviderType = PriceProviderType.Generic
        };

        var whitespaceUrl = missingUrl with { Url = "   " };

        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync(missingUrl));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync(whitespaceUrl));
    }

    [Fact]
    public async Task RunAsync_ShouldThrowArgumentException_WhenDisplayNameIsNullOrEmpty()
    {
        // Arrange
        var mockProvider = new Mock<IPriceProvider>();
        var mockStorage = new Mock<IPriceStorage>();
        var mockNotifier = new Mock<INotificationService>();
        var mockMonitoring = new Mock<IMonitoringService>();
        var mockLogger = new Mock<ILogger<PriceTracker>>();

        var tracker = new PriceTracker(
            mockProvider.Object,
            mockStorage.Object,
            mockNotifier.Object,
            mockMonitoring.Object,
            mockLogger.Object);

        // Act & Assert
        var missingName = new ProductConfig
        {
            Id = "iphone-17",
            DisplayName = "",
            Url = "https://example.com",
            CssSelector = "span.price",
            ProviderType = PriceProviderType.Generic
        };

        var whitespaceName = missingName with { DisplayName = "   " };

        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync(missingName));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync(whitespaceName));
    }
}