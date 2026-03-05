using OneNoteAnalyzeAddIn.Ui.ViewModels;

namespace OneNoteAnalyzeAddIn.Ui;

public sealed class CompanionWindowManager
{
    private readonly CompanionViewModel _viewModel;
    private CompanionWindow? _window;

    public CompanionWindowManager(CompanionViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public CompanionViewModel ViewModel => _viewModel;

    public void Show()
    {
        if (_window is null || !_window.IsLoaded)
        {
            _window = new CompanionWindow(_viewModel);
        }

        _window.Show();
        _window.Activate();
    }
}
