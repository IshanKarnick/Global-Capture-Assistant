using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GlobalCaptureAssistant.Platform;

/// <summary>
/// Enables Windows acrylic blur-behind on a WPF window via DWM P/Invoke.
/// Works on Windows 10 1903+ and Windows 11.
/// </summary>
internal static class DwmBlurHelper
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── Structs ───────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor; // ABGR
        public int AnimationId;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    }

    // DWMWA_SYSTEMBACKDROP_TYPE values (Windows 11 22H2+)
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33; // Windows 11+
    private const int DWMSBT_DISABLE = 1;
    private const int DWMSBT_MAINWINDOW = 2;   // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed Mica

    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_ROUND = 2;      // Standard rounded corners
    private const int DWMWCP_ROUNDSMALL = 3; // Smaller radius

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from the window's Loaded event.
    /// Applies acrylic blur on Win11 22H2+ (DWM system backdrop),
    /// falls back to SetWindowCompositionAttribute acrylic on older builds.
    /// </summary>
    /// <param name="window">The WPF window to enable blur on.</param>
    /// <param name="tintColorAbgr">
    /// Gradient colour in ABGR format used for the legacy acrylic path.
    /// E.g. 0xA0F5F0EE = alpha 160, B=245, G=240, R=238 (light warm white).
    /// </param>
    public static void EnableAcrylic(Window window, uint tintColorAbgr = 0x99F0EEEC)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Round the window corners at the OS level (removes double-border artifact)
        int roundCorners = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorners, sizeof(int));

        // Try Windows 11 22H2+ DWM system backdrop first
        if (TrySetSystemBackdrop(hwnd, DWMSBT_TRANSIENTWINDOW))
            return;

        // Fall back to SetWindowCompositionAttribute acrylic (Win10/Win11 older)
        SetAccentPolicy(hwnd, AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND, tintColorAbgr);
    }

    /// <summary>Enable plain blur-behind (no acrylic tint) as a last resort.</summary>
    public static void EnableBlurBehind(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetAccentPolicy(hwnd, AccentState.ACCENT_ENABLE_BLURBEHIND, 0x01000000);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool TrySetSystemBackdrop(IntPtr hwnd, int backdropType)
    {
        try
        {
            int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            return result == 0; // S_OK
        }
        catch
        {
            return false;
        }
    }

    private static void SetAccentPolicy(IntPtr hwnd, AccentState state, uint gradientColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = state,
            AccentFlags = 0x20,                   // draw luminosity
            GradientColor = (int)gradientColor,
        };

        int accentStructSize = Marshal.SizeOf(accent);
        IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr,
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
