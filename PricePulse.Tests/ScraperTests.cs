using PricePulse.Core;

namespace PricePulse.Tests;

public class ScraperTests
{
    [Fact]
    public async Task GetPriceAsync_ShouldReturnSuccess_WhenUrlIsValid()
    {
        // Arrange 
        var scraper = new PlaywrightScraper();
        var url = "https://www.microsoft.com";

        // Act 
        // Поки що наш метод повертає 0, але ми перевіряємо, що він не падає з помилкою
        var result = await scraper.GetPriceAsync(url);

        // Assert
        Assert.True(result >= 0, "Price should be a positive number");
    }
}