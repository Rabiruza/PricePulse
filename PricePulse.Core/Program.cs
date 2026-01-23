using PricePulse.Core;
using PricePulse.Core.Services;
using PricePulse.Core.Interfaces;

Console.WriteLine("🚀 Starting PricePulse: Full SOLID Edition");

// Composition Root
IPriceProvider provider = new WebPriceExtractor();
IPriceStorage storage = new PriceStorage();
INotificationService notifier = new TelegramNotificationService();
IMonitoringService monitoring = new PrometheusMonitoringService(); // New service

var tracker = new PriceTracker(provider, storage, notifier, monitoring);

await tracker.RunAsync("https://www.apple.com/iphone-17/", "iPhone 17");