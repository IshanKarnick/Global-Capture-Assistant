using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OneNoteAnalyzeAddIn.Models;
using OneNoteAnalyzeAddIn.Ui.Commands;

namespace OneNoteAnalyzeAddIn.Ui.ViewModels;

public sealed class CompanionViewModel : INotifyPropertyChanged
{
    private AnalysisViewState _state = AnalysisViewState.Idle;
    private string _statusText = "Ready";
    private string _resultText = "Capture a region from OneNote to begin analysis.";
    private string _metadataText = "No page metadata yet.";
    private string _errorText = string.Empty;
    private string _noticeText = string.Empty;
    private Func<Task>? _retryAction;
    private bool _canRetry;

    public CompanionViewModel()
    {
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AnalysisHistoryItem> History { get; } = [];

    public AnalysisViewState State
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

    public string MetadataText
    {
        get => _metadataText;
        set => SetField(ref _metadataText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        set => SetField(ref _errorText, value);
    }

    public string NoticeText
    {
        get => _noticeText;
        set
        {
            if (SetField(ref _noticeText, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNotice)));
            }
        }
    }

    public bool HasNotice => !string.IsNullOrWhiteSpace(NoticeText);

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

    public ICommand RetryCommand { get; }

    public void SetRetryAction(Func<Task>? retryAction)
    {
        _retryAction = retryAction;
        CanRetry = retryAction is not null;
    }

    public void AppendHistory(string title, string summary)
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

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
