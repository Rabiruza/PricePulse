namespace PricePulse.Core.Interfaces;

/// <summary> Sends notifications to users or systems. </summary>
public interface IMonitoringService
{
    Task PushMetricAsync(string model, decimal price);
}