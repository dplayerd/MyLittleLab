namespace LoggingDemo.Logging;

public static class MemoryLogData
{
    public static List<string> InfoLogs { get; } = new();
    public static List<string> WarningLogs { get; } = new();
    public static List<string> ErrorLogs { get; } = new();
    public static List<string> TraceLogs { get; } = new();
    public static List<string> DebugLogs { get; } = new();
}
