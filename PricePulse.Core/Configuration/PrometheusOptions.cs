namespace PricePulse.Core.Configuration;

public class PrometheusOptions
{
    public const string SectionName = "Prometheus";
    
    public string PushGatewayUrl { get; set; } = "http://localhost:9091";
    
    public string JobName { get; set; } = "price_pulse_job";
    
    public string MetricName { get; set; } = "iphone_price_usd";
}
