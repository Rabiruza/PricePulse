using System.Text.Json;
using Microsoft.Extensions.Logging;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class PriceStorage : IPriceStorage
{
    private const string HistoryFile = "price_history.json";
    private readonly ILogger<PriceStorage> _logger;

    public PriceStorage(ILogger<PriceStorage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetLastPriceAsync()
    {
        if (!File.Exists(HistoryFile))
        {
            _logger.LogDebug("Price history file not found: {HistoryFile}. Returning 0.", HistoryFile);
            return 0;
        }

        try
        {
            var json = await File.ReadAllTextAsync(HistoryFile);
            var price = JsonSerializer.Deserialize<decimal>(json);
            _logger.LogDebug("Retrieved last price from storage: ${Price}", price);
            return price;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize price from {HistoryFile}. File may be corrupted.", HistoryFile);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading price history from {HistoryFile}", HistoryFile);
            return 0;
        }
    }

    public async Task SavePriceAsync(decimal price)
    {
        try
        {
            var json = JsonSerializer.Serialize(price);
            await File.WriteAllTextAsync(HistoryFile, json);
            _logger.LogDebug("Saved price ${Price} to {HistoryFile}", price, HistoryFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save price ${Price} to {HistoryFile}", price, HistoryFile);
            throw; // Re-throw as this is a critical operation
        }
    }
}