using PricePulse.Core.Interfaces;

namespace PricePulse.Core;

public class PriceTracker
{
    private readonly IPriceProvider _priceProvider;
    private readonly IPriceStorage _priceStorage;
    private readonly INotificationService _notificationService;
    private readonly IMonitoringService _monitoringService; // Нова залежність

    public PriceTracker(
        IPriceProvider priceProvider, 
        IPriceStorage priceStorage, 
        INotificationService notificationService,
        IMonitoringService monitoringService)
    {
        _priceProvider = priceProvider;
        _priceStorage = priceStorage;
        _notificationService = notificationService;
        _monitoringService = monitoringService;
    }

    public async Task RunAsync(string url, string modelName)
    {
        decimal currentPrice = await _priceProvider.GetPriceAsync(url);
        if (currentPrice <= 0) return;

        // Викликаємо моніторинг
        await _monitoringService.PushMetricAsync(modelName, currentPrice);

        decimal lastPrice = await _priceStorage.GetLastPriceAsync();

        if (lastPrice == 0)
        {
            await _notificationService.SendAsync($"🤖 Started tracking {modelName}: ${currentPrice}");
        }
        else if (currentPrice != lastPrice)
        {
            string emoji = currentPrice < lastPrice ? "📉 SALE!" : "📈 Price update:";
            await _notificationService.SendAsync($"{emoji} {modelName}: ${currentPrice} (was ${lastPrice})");
        }

        await _priceStorage.SavePriceAsync(currentPrice);
    }
}