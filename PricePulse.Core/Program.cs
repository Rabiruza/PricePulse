
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PricePulse.Core;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;
using PricePulse.Core.Services;

// Build configuration
// Use AppContext.BaseDirectory to find the executable directory
var basePath = AppContext.BaseDirectory;
var configuration = new ConfigurationBuilder()
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // Make optional for CI/CD
    .AddEnvironmentVariables() // Environment variables override appsettings.json
    .Build();

// Set up services
var services = new ServiceCollection();

// Configuration
services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
services.Configure<PrometheusOptions>(configuration.GetSection(PrometheusOptions.SectionName));
services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
services.Configure<WebScrapingOptions>(configuration.GetSection(WebScrapingOptions.SectionName));
services.Configure<RetryPolicyOptions>(configuration.GetSection(RetryPolicyOptions.SectionName));
services.Configure<TrackingOptions>(configuration.GetSection(TrackingOptions.SectionName));

// Logging
services.AddLogging(builder =>
{
    builder
        .AddConsole()
        .AddConfiguration(configuration.GetSection("Logging"));
});

// HttpClient with named clients
services.AddHttpClient("Prometheus", client =>
{
    var options = configuration.GetSection(PrometheusOptions.SectionName).Get<PrometheusOptions>() ?? new PrometheusOptions();
    client.BaseAddress = new Uri(options.PushGatewayUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

services.AddHttpClient("Telegram", client =>
{
    var options = configuration.GetSection(TelegramOptions.SectionName).Get<TelegramOptions>() ?? new TelegramOptions();
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// Register services
services.AddTransient<IPriceProvider, WebPriceExtractor>();
services.AddTransient<IPriceStorage, PriceStorage>();
services.AddTransient<INotificationService, TelegramNotificationService>();
services.AddTransient<IMonitoringService, PrometheusMonitoringService>();
services.AddTransient<PriceTracker>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Starting PricePulse");

try
{
    // Get tracking configuration (for now, using first product from config)
    TrackingOptions trackingOptions =
        configuration.GetSection(TrackingOptions.SectionName).Get<TrackingOptions>() ?? new TrackingOptions();

    List<ProductConfig> products = trackingOptions.Products;

    if (products.Count == 0)
    {
        logger.LogError(
            "No products configured. Add at least one entry under {Section}:{Key} in appsettings.json.",
            TrackingOptions.SectionName,
            nameof(TrackingOptions.Products));
        Environment.ExitCode = 1;
        return;
    }

    PriceTracker tracker = serviceProvider.GetRequiredService<PriceTracker>();

    foreach (ProductConfig product in products)
    {
        if (string.IsNullOrWhiteSpace(product.Url))
        {
            logger.LogError("Product {ProductId} has no URL configured", product.Id);
            Environment.ExitCode = 1;
            continue;
        }

        await tracker.RunAsync(product);
    }
    
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
finally
{
    // Dispose service provider
    if (serviceProvider is IDisposable disposable)
    {
        disposable.Dispose();
    }
}