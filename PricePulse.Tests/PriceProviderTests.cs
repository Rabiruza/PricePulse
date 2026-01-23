using PricePulse.Core;

namespace PricePulse.Tests;

public class PriceProviderTests
{
    [Fact]
    public async Task GetPriceAsync_ShouldReturnSuccess_WhenUrlIsValid()
    {
        // Arrange 
        var priceProvider = new IPriceProvider();
        var url = "https://www.microsoft.com";

        // Act 
        // Поки що наш метод повертає 0, але ми перевіряємо, що він не падає з помилкою
        var result = await priceProvider.GetPriceAsync(url);

        // Assert
        Assert.True(result >= 0, "Price should be a positive number");
    }
}