namespace PricePulse.Core.Interfaces;

/// <summary>
/// Provides an abstraction for sending notifications to users or systems.
/// Implementations can vary (e.g., email, SMS, push notifications) without affecting consumers.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a message asynchronously.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    Task SendAsync(string message);
}