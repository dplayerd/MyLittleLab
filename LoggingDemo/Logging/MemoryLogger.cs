using Microsoft.Extensions.Logging;

namespace LoggingDemo.Logging;

public class MemoryLogger : ILogger
{
    private readonly string _category;

    public MemoryLogger(string category)
    {
        _category = category;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        //return default!;
        return MemoryScope.Push(state);

    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) 
            return;

        // 讀出目前所有 scope，並以可讀字串加入輸出
        var scopes = MemoryScope.Current;
        string scopeText = scopes.Any()
            ? $"[{string.Join(" => ", scopes.Select(s => s?.ToString()))}] "
            : string.Empty;

        string message = $"{DateTime.Now:HH:mm:ss} [{logLevel}] {_category} {scopeText}- {formatter(state, exception)}";

        switch (logLevel)
        {
            case LogLevel.Information:
                MemoryLogData.InfoLogs.Add(message);
                break;
            case LogLevel.Warning:
                MemoryLogData.WarningLogs.Add(message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                MemoryLogData.ErrorLogs.Add(message);
                break;
        }
    }
}
