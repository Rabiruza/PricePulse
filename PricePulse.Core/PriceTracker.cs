using PricePulse.Core.Interfaces;

namespace PricePulse.Core;

public class PriceTracker
{
    private readonly IPriceProvider _priceProvider;
    private readonly IPriceStorage _priceStorage;
    private readonly INotificationService _notificationService;

    public PriceTracker(
        IPriceProvider priceProvider, 
        IPriceStorage priceStorage, 
        INotificationService notificationService)
    {
        _priceProvider = priceProvider;
        _priceStorage = priceStorage;
        _notificationService = notificationService;
    }

    public async Task RunAsync(string url, string modelName)
    {
        decimal currentPrice = await _priceProvider.GetPriceAsync(url);
        if (currentPrice <= 0) return;

        decimal lastPrice = await _priceStorage.GetLastPriceAsync();

        if (lastPrice == 0)
        {
            await _notificationService.SendAsync($"🤖 Tracking started for {modelName} at ${currentPrice}");
        }
        else if (currentPrice != lastPrice)
        {
            string status = currentPrice < lastPrice ? "📉 SALE!" : "📈 Price up:";
            await _notificationService.SendAsync($"{status} {modelName}\nNew: ${currentPrice} (Was: ${lastPrice})");
        }

        await _priceStorage.SavePriceAsync(currentPrice);
    }
}