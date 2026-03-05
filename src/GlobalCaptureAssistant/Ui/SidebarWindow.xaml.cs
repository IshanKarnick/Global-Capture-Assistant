using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using GlobalCaptureAssistant.Platform;
using GlobalCaptureAssistant.Ui.ViewModels;

namespace GlobalCaptureAssistant.Ui;

public partial class SidebarWindow : Window
{
    private bool _allowClose;

    public SidebarWindow(SidebarViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            DwmBlurHelper.EnableAcrylic(this, 0x99F2F0EE);
            SnapToRight();
        };
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private void SnapToRight()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 16);
        Top = area.Top + 16;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
