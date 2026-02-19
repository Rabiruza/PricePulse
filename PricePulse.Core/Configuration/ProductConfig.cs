using System.ComponentModel.DataAnnotations;

namespace PricePulse.Core.Configuration;

/// <summary>
/// Configuration model for a product to track, including scraping settings and alert preferences.
/// </summary>
public sealed record ProductConfig
{
    /// <summary>
    /// Unique identifier for the product (e.g., "iphone-17-pro-256gb").
    /// Used for storage naming and tracking.
    /// </summary>
    [Required(ErrorMessage = "Product Id is required")]
    [RegularExpression(@"^[a-z0-9\-_]+$", ErrorMessage = "Product Id must contain only lowercase letters, numbers, hyphens, and underscores")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "iPhone 17 Pro 256GB").
    /// </summary>
    [Required(ErrorMessage = "Display name is required")]
    [MinLength(1, ErrorMessage = "Display name cannot be empty")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Target URL for price scraping.
    /// </summary>
    [Required(ErrorMessage = "URL is required")]
    [Url(ErrorMessage = "URL must be a valid HTTP or HTTPS URL")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// CSS selector for the price element.
    /// </summary>
    [Required(ErrorMessage = "CSS selector is required")]
    [MinLength(1, ErrorMessage = "CSS selector cannot be empty")]
    public string CssSelector { get; init; } = string.Empty;

    /// <summary>
    /// Type of price provider strategy to use for this product.
    /// </summary>
    [Required(ErrorMessage = "Provider type is required")]
    public PriceProviderType ProviderType { get; init; } = PriceProviderType.Generic;

    /// <summary>
    /// Optional target price. If set, alerts can trigger when price <= this value.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Target price must be non-negative")]
    public decimal? TargetPrice { get; init; }

    /// <summary>
    /// Optional 3-letter ISO currency code (e.g., "USD", "EUR").
    /// </summary>
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a valid 3-letter ISO currency code")]
    public string? Currency { get; init; }
}