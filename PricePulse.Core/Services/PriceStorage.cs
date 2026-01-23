using System.Text.Json;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class PriceStorage : IPriceStorage
{
    private const string HistoryFile = "price_history.json";

    public async Task<decimal> GetLastPriceAsync()
    {
        if (!File.Exists(HistoryFile)) return 0;
        try {
            var json = await File.ReadAllTextAsync(HistoryFile);
            return JsonSerializer.Deserialize<decimal>(json);
        } catch { return 0; }
    }

    public async Task SavePriceAsync(decimal price)
    {
        var json = JsonSerializer.Serialize(price);
        await File.WriteAllTextAsync(HistoryFile, json);
    }
}