using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace LoggingDemo.Logging;

public class FileLogProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private bool _disposed;

    public FileLogProvider(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _filePath, _lock);

    public void Dispose()
    {
        _disposed = true;
    }

    private class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly string _filePath;
        private readonly object _lock;

        public FileLogger(string category, string filePath, object @lock)
        {
            _category = category;
            _filePath = filePath;
            _lock = @lock;
        }

        public IDisposable BeginScope<TState>(TState state) => MemoryScope.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var scopes = MemoryScope.Current;
            string scopeText = scopes.Any()
                ? $"[{string.Join(" => ", scopes.Select(s => s?.ToString()))}] "
                : string.Empty;

            string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {_category} {scopeText}- {formatter(state, exception)}";

            lock (_lock)
            {
                File.AppendAllText(_filePath, message + Environment.NewLine);
            }
        }
    }
}