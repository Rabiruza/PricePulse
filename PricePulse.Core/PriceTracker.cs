using Microsoft.Extensions.Logging;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core;

public class PriceTracker
{
    private readonly IPriceProvider _priceProvider;
    private readonly IPriceStorage _priceStorage;
    private readonly INotificationService _notificationService;
    private readonly IMonitoringService _monitoringService;
    private readonly ILogger<PriceTracker> _logger;

    public PriceTracker(
        IPriceProvider priceProvider, 
        IPriceStorage priceStorage, 
        INotificationService notificationService,
        IMonitoringService monitoringService,
        ILogger<PriceTracker> logger)
    {
        _priceProvider = priceProvider ?? throw new ArgumentNullException(nameof(priceProvider));
        _priceStorage = priceStorage ?? throw new ArgumentNullException(nameof(priceStorage));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync(ProductConfig product)
    {
        if (product is null)
        {
            _logger.LogError("Product cannot be null");
            throw new ArgumentNullException(nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.Url))
        {
            _logger.LogError("Product {ProductId} has no URL configured", product.Id);
            throw new ArgumentException("Product URL cannot be null or empty", nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.DisplayName))
        {
            _logger.LogError("Product {ProductId} has no display name configured", product.Id);
            throw new ArgumentException("Product display name cannot be null or empty", nameof(product));
        }

        string modelName = product.DisplayName;
        string url = product.Url;

        _logger.LogInformation("Starting price check for {ModelName} at {Url}", modelName, url);

        try
        {
            decimal currentPrice = await _priceProvider.GetPriceAsync(product);
            
            if (currentPrice <= 0)
            {
                _logger.LogWarning("Failed to retrieve price for {ModelName} from {Url}. Price returned: {Price}", 
                    modelName, url, currentPrice);
                return;
            }

            _logger.LogInformation("Current price for {ModelName}: ${Price}", modelName, currentPrice);

            // Push metric (non-critical, failures are logged but don't stop execution)
            try
            {
                await _monitoringService.PushMetricAsync(modelName, currentPrice);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push metric for {ModelName}, continuing with price tracking", modelName);
            }

            decimal lastPrice = await _priceStorage.GetLastPriceAsync();

            if (lastPrice == 0)
            {
                _logger.LogInformation("First run for {ModelName}, sending initial notification", modelName);
                await _notificationService.SendAsync($"🤖 Started tracking {modelName}: ${currentPrice}");
            }
            else if (currentPrice < lastPrice)
            {
                string message = $"📉 SALE! {modelName}: ${currentPrice} (was ${lastPrice})";
                _logger.LogInformation("Price dropped for {ModelName}: ${OldPrice} -> ${NewPrice}",
                    modelName, lastPrice, currentPrice);
                await _notificationService.SendAsync(message);
            }
            else
            {
                _logger.LogDebug("Price unchanged for {ModelName}: ${Price}", modelName, currentPrice);
            }

            await _priceStorage.SavePriceAsync(currentPrice);
            _logger.LogInformation("Successfully completed price tracking for {ModelName}", modelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while tracking price for {ModelName} at {Url}", modelName, url);
            throw;
        }
    }
}