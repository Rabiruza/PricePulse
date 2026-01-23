using System.Text;
using Microsoft.Extensions.Logging;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class PrometheusMonitoringService : IMonitoringService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrometheusMonitoringService> _logger;
    private bool _disposed = false;

    public PrometheusMonitoringService(ILogger<PrometheusMonitoringService> logger)
    {
        _httpClient = new HttpClient();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PushMetricAsync(string model, decimal price)
    {
        try 
        {
            // Formatting for the Prometheus Pushgateway
            var body = $"iphone_price_usd{{model=\"{model}\"}} {(double)price}\n";
            var content = new StringContent(body, Encoding.UTF8);
            
            // Sending to the local port (Docker)
            var response = await _httpClient.PostAsync("http://localhost:9091/metrics/job/price_pulse_job", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("📊 Metric pushed successfully: {Model} = ${Price}", model, price);
            }
            else
            {
                _logger.LogWarning("Failed to push metric. Status code: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        { 
            // Log monitoring failures but don't throw - monitoring should not break the main flow
            _logger.LogWarning(ex, "Failed to push metric to Prometheus. This is non-critical and will be ignored.");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}