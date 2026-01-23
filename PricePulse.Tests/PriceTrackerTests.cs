using Microsoft.Extensions.Logging;
using PricePulse.Core;
using PricePulse.Core.Interfaces;
using PricePulse.Core.Services;
using Moq;
using Xunit;

namespace PricePulse.Tests;

public class PriceProviderTests
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
        var url = "https://www.microsoft.com";

        // Act 
        // This is an integration test that requires Playwright browsers to be installed
        // It verifies that WebPriceExtractor doesn't crash and returns a non-negative value
        var result = await priceProvider.GetPriceAsync(url);

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
        mockProvider.Setup(p => p.GetPriceAsync(It.IsAny<string>()))
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
        await tracker.RunAsync("http://example.com/iphone", "iPhone 17");

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
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync(null!, "iPhone 17"));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync("", "iPhone 17"));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync("   ", "iPhone 17"));
    }

    [Fact]
    public async Task RunAsync_ShouldThrowArgumentException_WhenModelNameIsNullOrEmpty()
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
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync("https://example.com", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync("https://example.com", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => tracker.RunAsync("https://example.com", "   "));
    }
}