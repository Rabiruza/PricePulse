using Microsoft.Playwright;
using Xunit;

public class PlaywrightSmokeTest
{
    [Fact]
    public async Task ShouldLaunchChromium()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync("https://example.com");
    }
}