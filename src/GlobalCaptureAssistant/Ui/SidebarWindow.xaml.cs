using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
            DwmBlurHelper.EnableAcrylic(this, 0x22F2F0EE);
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
        Top = Math.Max(area.Top, area.Bottom - Height - 16);
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

    private async void SuggestedPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SidebarViewModel viewModel || sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        if (button.Content is string prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            await viewModel.SendSuggestedPromptAsync(prompt).ConfigureAwait(true);
        }
    }

    private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (DataContext is SidebarViewModel viewModel && viewModel.SendChatCommand.CanExecute(null))
        {
            viewModel.SendChatCommand.Execute(null);
            e.Handled = true;
        }
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
