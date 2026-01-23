namespace PricePulse.Core.Configuration;

public class RetryPolicyOptions
{
    public const string SectionName = "RetryPolicy";
    
    public int MaxRetries { get; set; } = 3;
    
    public int DelaySeconds { get; set; } = 2;
    
    public double BackoffMultiplier { get; set; } = 2.0;
}
