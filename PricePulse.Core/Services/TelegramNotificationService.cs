using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using PricePulse.Core.Configuration;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class TelegramNotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramOptions _options;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramOptions> options,
        IOptions<RetryPolicyOptions> retryOptions,
        ILogger<TelegramNotificationService> logger)
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
                    _logger.LogWarning("Retry {RetryCount} for Telegram notification after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                });
    }

    public async Task SendAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Attempted to send empty Telegram message");
            return;
        }

        var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
        {
            _logger.LogWarning("Telegram credentials not configured. TELEGRAM_TOKEN or TELEGRAM_CHAT_ID is missing.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Telegram");
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            
            var url = $"{_options.BaseUrl}{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
            
            var response = await _retryPolicy.ExecuteAsync(async () =>
                await client.GetAsync(url));
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram notification sent successfully");
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to send Telegram notification. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram notification");
            // Don't throw - notification failures shouldn't break price tracking
        }
    }
}