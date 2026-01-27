namespace PricePulse.Core.Interfaces;

/// <summary> Sends notifications to users or systems. </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a message asynchronously.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    Task SendAsync(string message);
}