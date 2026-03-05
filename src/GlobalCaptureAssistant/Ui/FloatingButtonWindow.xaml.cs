using System.Windows;
using System.Windows.Input;
using GlobalCaptureAssistant.Platform;

namespace GlobalCaptureAssistant.Ui;

public partial class FloatingButtonWindow : Window
{
    private System.Windows.Point _mouseDownPos;
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
        _mouseDownPos = e.GetPosition(this);
        _isPointerDown = true;
        _isDragging = false;
    }

    private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPointerDown || e.LeftButton != MouseButtonState.Pressed || _isDragging)
        {
            return;
        }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _mouseDownPos.X) <= 4 && Math.Abs(pos.Y - _mouseDownPos.Y) <= 4)
        {
            return;
        }

        _isDragging = true;
        try
        {
            DragMove();
        }
        finally
        {
            _isPointerDown = false;
        }
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPointerDown = false;
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isDragging)
        {
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }

        _isDragging = false;
    }
}
