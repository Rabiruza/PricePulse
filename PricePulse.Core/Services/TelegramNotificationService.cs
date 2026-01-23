using Microsoft.Extensions.Logging;
using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class TelegramNotificationService : INotificationService, IDisposable
{
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed = false;

    public TelegramNotificationService(ILogger<TelegramNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();
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
            var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
            var response = await _httpClient.GetAsync(url);
            
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}