using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace PricePulse.Tests;

public class PriceParsingTests
{
    // This regex matches the one used in WebPriceExtractor
    private static readonly Regex PriceRegex = new(
        @"(?:\$|USD|€|£)?\s*(?:From\s+)?(\d+(?:\.\d{1,2})?)",
        RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("$799", 799)]
    [InlineData("From $799", 799)]
    [InlineData("$799.99", 799.99)]
    [InlineData("799", 799)]
    [InlineData("799.99", 799.99)]
    [InlineData("USD 799", 799)]
    [InlineData("€799", 799)]
    [InlineData("£799", 799)]
    [InlineData("From $799.50", 799.50)]
    [InlineData("  $  799  ", 799)] // With extra whitespace
    public void PriceRegex_ShouldExtractPrice_FromVariousFormats(string priceText, decimal expectedPrice)
    {
        // Act
        var match = PriceRegex.Match(priceText);
        
        // Assert
        Assert.True(match.Success, $"Regex should match: {priceText}");
        Assert.True(match.Groups.Count > 1, "Should have at least one capture group");
        
        var priceValue = match.Groups[1].Value;
        var parsed = decimal.Parse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture);
        
        Assert.Equal(expectedPrice, parsed);
    }

    [Theory]
    [InlineData("Save $100 on $799", 100)] // Should extract first number
    [InlineData("$799 - $899", 799)] // Should extract first number
    [InlineData("Price: $799.99", 799.99)]
    [InlineData("Starting at $799", 799)]
    public void PriceRegex_ShouldExtractFirstPrice_FromComplexText(string priceText, decimal expectedPrice)
    {
        // Act
        var match = PriceRegex.Match(priceText);
        
        // Assert
        Assert.True(match.Success, $"Regex should match: {priceText}");
        
        var priceValue = match.Groups[1].Value;
        var parsed = decimal.Parse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture);
        
        Assert.Equal(expectedPrice, parsed);
    }

    [Theory]
    [InlineData("Free")]
    [InlineData("Contact us")]
    [InlineData("N/A")]
    [InlineData("")]
    [InlineData("No price available")]
    public void PriceRegex_ShouldNotMatch_InvalidPriceText(string priceText)
    {
        // Act
        var match = PriceRegex.Match(priceText);
        
        // Assert
        Assert.False(match.Success, $"Regex should not match: {priceText}");
    }

    [Fact]
    public void PriceRegex_ShouldHandleDecimalPrices()
    {
        // Arrange
        var testCases = new[]
        {
            ("$799.99", 799.99m),
            ("$799.5", 799.5m),
            ("$799.50", 799.50m),
            ("799.99", 799.99m)
        };

        foreach (var (priceText, expectedPrice) in testCases)
        {
            // Act
            var match = PriceRegex.Match(priceText);
            
            // Assert
            Assert.True(match.Success, $"Should match: {priceText}");
            
            var priceValue = match.Groups[1].Value;
            var parsed = decimal.Parse(priceValue, NumberStyles.Number, CultureInfo.InvariantCulture);
            
            Assert.Equal(expectedPrice, parsed);
        }
    }
}
