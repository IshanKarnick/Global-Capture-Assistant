using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace GlobalCaptureAssistant.Platform;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x7331;
    private HwndSource? _source;
    private bool _isRegistered;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event EventHandler? HotkeyPressed;

    public bool RegisterCtrlShiftQ()
    {
        EnsureSource();
        if (_source is null)
        {
            return false;
        }

        _isRegistered = RegisterHotKey(_source.Handle, HotkeyId, 0x0002 | 0x0004, 0x51);
        return _isRegistered;
    }

    public void Unregister()
    {
        if (_source is not null && _isRegistered)
        {
            _ = UnregisterHotKey(_source.Handle, HotkeyId);
            _isRegistered = false;
        }
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("GlobalCaptureAssistantHotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
