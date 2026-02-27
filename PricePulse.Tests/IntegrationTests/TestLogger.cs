using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace PricePulse.Tests.IntegrationTests.TestLogger;

public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output) => _output = output;

    IDisposable ILogger.BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine(formatter(state, exception));
    }
}