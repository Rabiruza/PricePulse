using PricePulse.Core;
using System.Text.Json;
using System.Text;

Console.WriteLine("🚀 Starting PricePulse: Apple Tracker Edition");

var priceProvider = new IPriceProvider();
string appleUrl = "https://www.apple.com/iphone-17/"; // URL for tracking
string historyFile = "price_history.json";

// 1. Get current price
decimal currentPrice = await priceProvider.GetPriceAsync(appleUrl);

if (currentPrice > 0)
{
    // 2. Monitoring (Prometheus) - only if running locally or if server is available
    try 
    {
        using var client = new HttpClient();
        var body = $"iphone_price_usd{{model=\"iphone-17\"}} {(double)currentPrice}\n";
        var content = new StringContent(body, Encoding.UTF8);
        var response = await client.PostAsync("http://localhost:9091/metrics/job/price_pulse_job", content);
        Console.WriteLine($"📊 Monitoring push status: {response.StatusCode}");
    }
    catch { /* Ignore prometheus errors in GitHub Actions */ }

    Console.WriteLine($"✅ Current iPhone 17 price: ${currentPrice}");

    // 3. Price history & Telegram Logic
    decimal lastPrice = 0;
    if (File.Exists(historyFile))
    {
        try {
            var json = File.ReadAllText(historyFile);
            lastPrice = JsonSerializer.Deserialize<decimal>(json);
            Console.WriteLine($"📊 Last recorded price: ${lastPrice}");
        } catch { lastPrice = 0; }
    }

    // Prepare notification message
    if (lastPrice > 0 && currentPrice != lastPrice)
    {
        string emoji = currentPrice < lastPrice ? "📉 SALE!" : "📈 Price increase:";
        string message = $"{emoji} iPhone 17 price changed!\nOld: ${lastPrice}\nNew: ${currentPrice}\nLink: {appleUrl}";
        
        Console.WriteLine(message);
        await SendTelegramNotification(message);
    }
    else if (lastPrice == 0)
    {
        await SendTelegramNotification($"🤖 First run! Tracking iPhone 17 at ${currentPrice}");
    }
    else
    {
        Console.WriteLine("↔️ Price is stable. No Telegram notification sent.");
    }

    // 4. Update history
    File.WriteAllText(historyFile, JsonSerializer.Serialize(currentPrice));
    Console.WriteLine("💾 Price history updated.");
}
else
{
    Console.WriteLine("❌ Could not retrieve price.");
}

// Function to send Telegram messages
async Task SendTelegramNotification(string message)
{
    var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
    var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
    {
        Console.WriteLine("⚠️ Telegram credentials not found. Skipping notification.");
        return;
    }

    try {
        using var client = new HttpClient();
        var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode) Console.WriteLine("📲 Telegram notification sent!");
    }
    catch (Exception ex) {
        Console.WriteLine($"❌ Failed to send Telegram: {ex.Message}");
    }
}