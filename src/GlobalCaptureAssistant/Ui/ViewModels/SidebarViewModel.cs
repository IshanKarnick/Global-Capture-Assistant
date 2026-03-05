using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GlobalCaptureAssistant.Config;
using GlobalCaptureAssistant.Models;
using GlobalCaptureAssistant.Ui.Commands;

namespace GlobalCaptureAssistant.Ui.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> DefaultModelOptions =
    [
        "gemini-3.1-pro-preview",
        "gemini-3-flash-preview",
        "gemini-3.1-flash-lite-preview"
    ];

    private static readonly IReadOnlyList<string> DefaultThinkingOptions =
    [
        "minimal",
        "low",
        "medium",
        "high"
    ];

    private AnalysisState _state = AnalysisState.Idle;
    private string _statusText = "Ready";
    private string _resultText = "Capture a region to analyze.";
    private string _contextText = "No capture context yet.";
    private string _errorText = string.Empty;
    private bool _canRetry;
    private Func<Task>? _retryAction;
    private string _selectedModelId = "gemini-3.1-pro-preview";
    private string _selectedThinkingLevel = "low";
    private bool _autoStartEnabled = true;
    private bool _suppressSettingsChanged;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsChanged;

    public SidebarViewModel()
    {
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);
    }

    public ObservableCollection<AnalysisHistoryItem> History { get; } = [];
    public IReadOnlyList<string> ModelOptions => DefaultModelOptions;
    public IReadOnlyList<string> ThinkingOptions => DefaultThinkingOptions;

    public AnalysisState State
    {
        get => _state;
        set => SetField(ref _state, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string ResultText
    {
        get => _resultText;
        set => SetField(ref _resultText, value);
    }

    public string ContextText
    {
        get => _contextText;
        set => SetField(ref _contextText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetField(ref _errorText, value);
    }

    public bool CanRetry
    {
        get => _canRetry;
        set
        {
            if (SetField(ref _canRetry, value))
            {
                (RetryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedModelId
    {
        get => _selectedModelId;
        set
        {
            if (SetField(ref _selectedModelId, value))
            {
                RaiseSettingsChanged();
            }
        }
    }

    public string SelectedThinkingLevel
    {
        get => _selectedThinkingLevel;
        set
        {
            if (SetField(ref _selectedThinkingLevel, value))
            {
                RaiseSettingsChanged();
            }
        }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (SetField(ref _autoStartEnabled, value))
            {
                RaiseSettingsChanged();
            }
        }
    }

    public ICommand RetryCommand { get; }

    public void SetRetryAction(Func<Task>? retryAction)
    {
        _retryAction = retryAction;
        CanRetry = retryAction is not null;
    }

    public void ApplySettings(AppSettings settings)
    {
        _suppressSettingsChanged = true;
        try
        {
            SelectedModelId = string.IsNullOrWhiteSpace(settings.ModelId)
                ? "gemini-3.1-pro-preview"
                : settings.ModelId;
            SelectedThinkingLevel = string.IsNullOrWhiteSpace(settings.ThinkingLevel)
                ? "low"
                : settings.ThinkingLevel;
            AutoStartEnabled = settings.AutoStartEnabled;
        }
        finally
        {
            _suppressSettingsChanged = false;
        }
    }

    public void AddHistory(string title, string summary)
    {
        History.Insert(0, new AnalysisHistoryItem(DateTimeOffset.Now, title, summary));
        while (History.Count > 20)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    private async Task RetryAsync()
    {
        if (_retryAction is null)
        {
            return;
        }

        await _retryAction().ConfigureAwait(true);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? member = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(member));
        return true;
    }

    private void RaiseSettingsChanged()
    {
        if (_suppressSettingsChanged)
        {
            return;
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
