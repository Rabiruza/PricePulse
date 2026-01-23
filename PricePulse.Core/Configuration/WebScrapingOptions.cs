namespace PricePulse.Core.Configuration;

public class WebScrapingOptions
{
    public const string SectionName = "WebScraping";
    
    public int SelectorTimeoutMs { get; set; } = 10000;
    
    public string UserAgent { get; set; } = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
}
