namespace PricePulse.Core.Interfaces;

public interface IMonitoringService
{
    Task PushMetricAsync(string model, decimal price);
}