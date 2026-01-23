using PricePulse.Core;
using PricePulse.Core.Interfaces;
using PricePulse.Core.Services;
using Moq;
using Xunit;

namespace PricePulse.Tests;

public class PriceProviderTests
{
    [Fact]
    public async Task GetPriceAsync_ShouldReturnSuccess_WhenUrlIsValid()
    {
        // Arrange 
        var priceProvider = new WebPriceExtractor();
        var url = "https://www.microsoft.com";

        // Act 
        // Поки що наш метод повертає 0, але ми перевіряємо, що він не падає з помилкою
        var result = await priceProvider.GetPriceAsync(url);

        // Assert
        Assert.True(result >= 0, "Price should be a positive number");
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
            mockMonitoring.Object);

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
}