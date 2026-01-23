using PricePulse.Core.Interfaces;

namespace PricePulse.Core.Services;

public class TelegramNotificationService : INotificationService
{
    public async Task SendAsync(string message)
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId)) return;

        using var client = new HttpClient();
        var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
        await client.GetAsync(url);
    }
}