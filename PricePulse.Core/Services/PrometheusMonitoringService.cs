using System.Text;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class PrometheusMonitoringService : IMonitoringService
{
    private readonly HttpClient _httpClient;

    public PrometheusMonitoringService()
    {
        _httpClient = new HttpClient();
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
                Console.WriteLine($"📊 Metric pushed: {price}");
        }
        catch 
        { 
            // Ignore monitoring issues in GitHub Actions, where no access to the localhost
        }
    }
}