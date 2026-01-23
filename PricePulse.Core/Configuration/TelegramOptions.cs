namespace PricePulse.Core.Configuration;

public class TelegramOptions
{
    public const string SectionName = "Telegram";
    
    public string BaseUrl { get; set; } = "https://api.telegram.org/bot";
    
    public int TimeoutSeconds { get; set; } = 30;
}
