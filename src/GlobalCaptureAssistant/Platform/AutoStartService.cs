using Microsoft.Win32;

namespace GlobalCaptureAssistant.Platform;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "GlobalCaptureAssistant";

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(EntryName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return;
        }

        key.SetValue(EntryName, $"\"{exePath}\"");
    }
}
