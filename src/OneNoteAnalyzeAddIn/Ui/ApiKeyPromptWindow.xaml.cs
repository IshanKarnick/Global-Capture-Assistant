using System.Windows;

namespace OneNoteAnalyzeAddIn.Ui;

public partial class ApiKeyPromptWindow : Window
{
    public ApiKeyPromptWindow()
    {
        InitializeComponent();
    }

    public string ApiKey => ApiKeyInput.Password.Trim();

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            MessageBox.Show(this, "API key cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
