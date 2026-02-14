using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Configuration;

public class StorageOptions
{
    public const string SectionName = "Storage";
    
    public string HistoryFile { get; set; } = "price_history.json";
}
