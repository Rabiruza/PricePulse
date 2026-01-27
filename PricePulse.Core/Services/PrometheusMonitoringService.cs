using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class PrometheusMonitoringService : IMonitoringService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PrometheusOptions _options;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<PrometheusMonitoringService> _logger;

    public PrometheusMonitoringService(
        IHttpClientFactory httpClientFactory,
        IOptions<PrometheusOptions> options,
        IOptions<RetryPolicyOptions> retryOptions,
        ILogger<PrometheusMonitoringService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var retryOpts = retryOptions?.Value ?? new RetryPolicyOptions();
        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: retryOpts.MaxRetries,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(retryOpts.DelaySeconds * Math.Pow(retryOpts.BackoffMultiplier, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for Prometheus push after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    public async Task PushMetricAsync(string model, decimal price)
    {
        try 
        {
            var client = _httpClientFactory.CreateClient("Prometheus");
            var url = $"{_options.PushGatewayUrl}/metrics/job/{_options.JobName}";
            
            // Formatting for the Prometheus Pushgateway
            var body = $"{_options.MetricName}{{model=\"{model}\"}} {(double)price}\n";
            var content = new StringContent(body, Encoding.UTF8);
            
            var response = await _retryPolicy.ExecuteAsync(async () =>
                await client.PostAsync(url, content));
            
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
}