using System.Windows;
using System.Windows.Input;
using GlobalCaptureAssistant.Platform;

namespace GlobalCaptureAssistant.Ui;

public partial class ApiKeyPromptWindow : Window
{
    public ApiKeyPromptWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DwmBlurHelper.EnableAcrylic(this, 0x99F2F0EE);
            ApiKeyInput.Focus();
        };
    }

    public string ApiKey => ApiKeyInput.Password.Trim();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            System.Windows.MessageBox.Show(this, "API key cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
