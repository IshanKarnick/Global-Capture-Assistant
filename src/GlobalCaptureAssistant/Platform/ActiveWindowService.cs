using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GlobalCaptureAssistant.Models;

namespace GlobalCaptureAssistant.Platform;

public sealed class ActiveWindowService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public ActiveWindowContext? TryGetContext()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var titleBuilder = new StringBuilder(512);
        _ = GetWindowText(handle, titleBuilder, titleBuilder.Capacity);
        _ = GetWindowThreadProcessId(handle, out var processId);

        string processName;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch
        {
            processName = "UnknownProcess";
        }

        var title = string.IsNullOrWhiteSpace(titleBuilder.ToString()) ? "Untitled Window" : titleBuilder.ToString();
        return new ActiveWindowContext(title, processName);
    }
}
