using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PricePulse.Core.Configuration;

/// <summary>
/// Root configuration for product tracking.
/// Binds to the "Tracking" section in configuration.
/// </summary>
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    /// <summary>
    /// List of products to track.
    /// </summary>
    [Required(ErrorMessage = "At least one product must be configured")]
    public List<ProductConfig> Products { get; init; } = new();
}