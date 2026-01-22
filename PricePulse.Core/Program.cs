using PricePulse.Core;
using System.Text.Json;
using System.Text;

Console.WriteLine("🚀 Starting PricePulse: Apple Tracker Edition");

var scraper = new PlaywrightScraper();
string appleUrl = "https://www.apple.com/iphone-17/";
string historyFile = "price_history.json";

// 1. Get current price
decimal currentPrice = await scraper.GetPriceAsync(appleUrl);

if (currentPrice > 0)
{
    // 2. Push to Prometheus Pushgateway using simple HTTP POST
    // This bypasses the problematic MetricPusher library
    try 
    {
        using var client = new HttpClient();
        // Format the data in Prometheus line protocol: metric_name{labels} value
        var body = $"iphone_price_usd{{model=\"iphone-17\"}} {(double)currentPrice}\n";
        var content = new StringContent(body, Encoding.UTF8);
        
        // Push to the Pushgateway endpoint directly
        var response = await client.PostAsync("http://localhost:9091/metrics/job/price_pulse_job", content);
        
        if (response.IsSuccessStatusCode)
            Console.WriteLine($"✅ Data pushed to Prometheus via HTTP: ${currentPrice}");
        else
            Console.WriteLine($"⚠️ Pushgateway returned: {response.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Monitoring alert: {ex.Message}");
    }

    Console.WriteLine($"✅ Current iPhone 17 price: ${currentPrice}");

    // 3. Price history logic
    decimal lastPrice = 0;
    if (File.Exists(historyFile))
    {
        var json = File.ReadAllText(historyFile);
        lastPrice = JsonSerializer.Deserialize<decimal>(json);
        Console.WriteLine($"📊 Last recorded price: ${lastPrice}");
    }

    if (lastPrice > 0)
    {
        if (currentPrice < lastPrice) Console.WriteLine("📉 SALE! The price has dropped!");
        else if (currentPrice > lastPrice) Console.WriteLine("📈 Price alert: It's getting more expensive.");
        else Console.WriteLine("↔️ Price is stable.");
    }

    File.WriteAllText(historyFile, JsonSerializer.Serialize(currentPrice));
    Console.WriteLine("💾 Price history updated.");
}
else
{
    Console.WriteLine("❌ Could not retrieve price.");
}