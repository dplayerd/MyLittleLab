using Microsoft.Extensions.Logging;

namespace LoggingDemo.Logging;

public class MemoryLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new MemoryLogger(categoryName);
    }

    public void Dispose() { }
}
