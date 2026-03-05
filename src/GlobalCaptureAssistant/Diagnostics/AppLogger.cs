using System.IO;

namespace GlobalCaptureAssistant.Diagnostics;

public sealed class AppLogger
{
    private readonly object _gate = new();
    private readonly string _path;

    public AppLogger()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlobalCaptureAssistant", "logs");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message} :: {exception}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:O}\t{level}\t{message}";
        lock (_gate)
        {
            File.AppendAllLines(_path, [line]);
        }
    }
}
