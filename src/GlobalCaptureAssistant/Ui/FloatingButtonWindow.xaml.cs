using System.Windows;
using System.Windows.Input;
using GlobalCaptureAssistant.Platform;

namespace GlobalCaptureAssistant.Ui;

public partial class FloatingButtonWindow : Window
{
    public FloatingButtonWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    public event EventHandler? CaptureRequested;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DwmBlurHelper.EnableAcrylic(this, 0x99F2F0EE);
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Top + 140;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureRequested?.Invoke(this, EventArgs.Empty);
    }
}
