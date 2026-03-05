using System.IO;

namespace OneNoteAnalyzeAddIn.Diagnostics;

public sealed class AppLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public AppLogger()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneNoteAnalyzeAddIn", "logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, $"addin-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void Info(string message, string? correlationId = null) => Write("INFO", message, correlationId);
    public void Warn(string message, string? correlationId = null) => Write("WARN", message, correlationId);
    public void Error(string message, Exception? ex = null, string? correlationId = null)
    {
        var merged = ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", merged, correlationId);
    }

    private void Write(string level, string message, string? correlationId)
    {
        var line = $"{DateTimeOffset.UtcNow:O}\t{level}\t{correlationId ?? "-"}\t{message}";
        lock (_lock)
        {
            File.AppendAllLines(_logPath, [line]);
        }
    }
}
