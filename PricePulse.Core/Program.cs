using Microsoft.Extensions.Logging;
using PricePulse.Core;
using PricePulse.Core.Services;
using PricePulse.Core.Interfaces;

// Set up logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("🚀 Starting PricePulse");

try
{
    // Create loggers for each service
    var providerLogger = loggerFactory.CreateLogger<WebPriceExtractor>();
    var storageLogger = loggerFactory.CreateLogger<PriceStorage>();
    var notifierLogger = loggerFactory.CreateLogger<TelegramNotificationService>();
    var monitoringLogger = loggerFactory.CreateLogger<PrometheusMonitoringService>();
    var trackerLogger = loggerFactory.CreateLogger<PriceTracker>();

    // Initialize services with logging
    IPriceProvider provider = new WebPriceExtractor(providerLogger);
    IPriceStorage storage = new PriceStorage(storageLogger);

    // 'using' works on the *static type* - these interfaces don't implement IDisposable.
    // Dispose the concrete implementations, but pass them to PriceTracker via the interfaces.
    using var notifierImpl = new TelegramNotificationService(notifierLogger);
    using var monitoringImpl = new PrometheusMonitoringService(monitoringLogger);
    INotificationService notifier = notifierImpl;
    IMonitoringService monitoring = monitoringImpl;

    var tracker = new PriceTracker(provider, storage, notifier, monitoring, trackerLogger);

    await tracker.RunAsync("https://www.apple.com/iphone-17/", "iPhone 17");
    
    logger.LogInformation("✅ PricePulse completed successfully");
}
catch (ArgumentException ex)
{
    logger.LogError(ex, "Invalid argument provided: {Message}", ex.Message);
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Fatal error occurred in PricePulse");
    Environment.ExitCode = 1;
}