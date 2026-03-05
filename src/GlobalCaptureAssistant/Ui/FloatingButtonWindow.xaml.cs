using System.Windows;
using System.Windows.Input;
using GlobalCaptureAssistant.Platform;

namespace GlobalCaptureAssistant.Ui;

public partial class FloatingButtonWindow : Window
{
    private System.Windows.Point _mouseDownScreen;
    private double _windowStartLeft;
    private double _windowStartTop;
    private bool _isPointerDown;
    private bool _isDragging;

    public FloatingButtonWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
    }

    public event EventHandler? CaptureRequested;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DwmBlurHelper.EnableAcrylic(this, 0x99F2F0EE);
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Top + 140;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownScreen = PointToScreen(e.GetPosition(this));
        _windowStartLeft = Left;
        _windowStartTop = Top;
        _isPointerDown = true;
        _isDragging = false;
        CaptureMouse();
    }

    private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPointerDown || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var screenPos = PointToScreen(e.GetPosition(this));
        var dx = screenPos.X - _mouseDownScreen.X;
        var dy = screenPos.Y - _mouseDownScreen.Y;

        if (!_isDragging && Math.Abs(dx) <= 3 && Math.Abs(dy) <= 3)
        {
            return;
        }

        _isDragging = true;
        Left = _windowStartLeft + dx;
        Top = _windowStartTop + dy;
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPointerDown)
        {
            ReleaseMouseCapture();
        }

        if (!_isDragging)
        {
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }

        _isPointerDown = false;
        _isDragging = false;
    }
}
