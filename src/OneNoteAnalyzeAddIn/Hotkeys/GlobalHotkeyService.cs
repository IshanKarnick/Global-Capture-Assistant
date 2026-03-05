using System.Windows.Interop;
using OneNoteAnalyzeAddIn.Diagnostics;

namespace OneNoteAnalyzeAddIn.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x4711;
    private readonly AppLogger _logger;
    private HwndSource? _source;
    private bool _isRegistered;
    private int _lastVirtualKey;
    private uint _lastModifiers;

    public GlobalHotkeyService(AppLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler? HotkeyPressed;

    public bool Register(uint modifiers, int virtualKey)
    {
        _lastModifiers = modifiers;
        _lastVirtualKey = virtualKey;

        EnsureSource();
        if (_source is null)
        {
            return false;
        }

        _isRegistered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, (uint)virtualKey);
        if (!_isRegistered)
        {
            _logger.Warn($"Failed to register hotkey modifiers={modifiers} vk={virtualKey}.");
        }

        return _isRegistered;
    }

    public bool TryRegisterCtrlShiftQ() => Register((uint)(NativeMethods.ModControl | NativeMethods.ModShift), 0x51);

    public void Unregister()
    {
        if (_source is not null && _isRegistered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _isRegistered = false;
        }
    }

    public void ReRegisterLast()
    {
        if (_lastVirtualKey == 0)
        {
            return;
        }

        Unregister();
        Register(_lastModifiers, _lastVirtualKey);
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("OneNoteAnalyzeAddInHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000) // WS_POPUP
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
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
