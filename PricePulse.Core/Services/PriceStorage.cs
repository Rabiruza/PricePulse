using System.Text.Json;

namespace PricePulse.Core.Services;

public class PriceStorage : IPriceStorage
{
    private readonly string _historyFile;
    private readonly ILogger<PriceStorage> _logger;

    public PriceStorage(IOptions<StorageOptions> options, ILogger<PriceStorage> logger)
    {
        _historyFile = options?.Value?.HistoryFile ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal> GetLastPriceAsync()
    {
        if (!File.Exists(_historyFile))
        {
            _logger.LogDebug("Price history file not found: {HistoryFile}. Returning 0.", _historyFile);
            return 0;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyFile);
            var price = JsonSerializer.Deserialize<decimal>(json);
            _logger.LogDebug("Retrieved last price from storage: ${Price}", price);
            return price;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize price from {HistoryFile}. File may be corrupted.", _historyFile);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading price history from {HistoryFile}", _historyFile);
            return 0;
        }
    }

    public async Task SavePriceAsync(decimal price)
    {
        try
        {
            var json = JsonSerializer.Serialize(price);
            await File.WriteAllTextAsync(_historyFile, json);
            _logger.LogDebug("Saved price ${Price} to {HistoryFile}", price, _historyFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save price ${Price} to {HistoryFile}", price, _historyFile);
            throw; // Re-throw as this is a critical operation
        }
    }
}