using System.Runtime.InteropServices;

namespace OneNoteAnalyzeAddIn.Hotkeys;

internal static class NativeMethods
{
    internal const int WmHotkey = 0x0312;
    internal const int ModAlt = 0x0001;
    internal const int ModControl = 0x0002;
    internal const int ModShift = 0x0004;
    internal const int ModWin = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
