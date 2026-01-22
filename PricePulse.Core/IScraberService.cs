namespace PricePulse.Core;

/// <summary>
/// Інтерфейс для сервісу збору даних з веб-сторінок.
/// Демонструє принцип інверсії залежностей.
/// </summary>
public interface IScraperService
{
    // Метод повертає ціну товару за вказаним URL
    Task<decimal> GetPriceAsync(string url);
}