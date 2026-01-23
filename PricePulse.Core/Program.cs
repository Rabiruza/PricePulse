using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PricePulse.Core;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;
using PricePulse.Core.Services;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
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
    // Get product configuration (for now, using first product from config)
    var productsSection = configuration.GetSection("Products");
    var products = new List<ProductConfiguration>();
    productsSection.Bind(products);
    
    // Fallback to default if no products configured
    if (products.Count == 0)
    {
        products.Add(new ProductConfiguration 
        { 
            Name = "iPhone 17", 
            Url = "https://www.apple.com/iphone-17/", 
            Selector = "span[data-pricing-product='iphone-17']" 
        });
    }
    
    var product = products.FirstOrDefault();
    if (product == null || string.IsNullOrWhiteSpace(product.Url))
    {
        logger.LogError("No product configuration found");
        Environment.ExitCode = 1;
        return;
    }

    var tracker = serviceProvider.GetRequiredService<PriceTracker>();
    await tracker.RunAsync(product.Url, product.Name);
    
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