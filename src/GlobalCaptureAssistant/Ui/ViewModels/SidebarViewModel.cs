using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GlobalCaptureAssistant.Config;
using GlobalCaptureAssistant.Models;
using GlobalCaptureAssistant.Ui.Commands;

namespace GlobalCaptureAssistant.Ui.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> DefaultProviderOptions =
    [
        "Gemini",
        "Groq"
    ];

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

    private static readonly IReadOnlyList<string> DefaultGroqModelOptions =
    [
        "meta-llama/llama-4-scout-17b-16e-instruct"
    ];

    private AnalysisState _state = AnalysisState.Idle;
    private string _statusText = "Ready";
    private string _resultText = "Capture a region to analyze.";
    private string _contextText = "No capture context yet.";
    private BitmapSource? _capturePreview;
    private BitmapSource? _generatedNotesPreview;
    private string _errorText = string.Empty;
    private bool _canRetry;
    private Func<Task>? _retryAction;
    private string _selectedTextProvider = "Gemini";
    private string _selectedAnnotationProvider = "Groq";
    private string _selectedModelId = "gemini-3.1-pro-preview";
    private string _selectedGroqModelId = "meta-llama/llama-4-scout-17b-16e-instruct";
    private string _selectedThinkingLevel = "low";
    private bool _autoStartEnabled = true;
    private bool _focusSidebarAfterCapture = true;
    private bool _suppressSettingsChanged;
    private string _chatInput = string.Empty;
    private Func<string, bool, Task>? _chatSendAction;
    private Func<Task>? _generateNotesAction;
    private Func<Task>? _annotateScreenAction;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsChanged;

    public SidebarViewModel()
    {
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);
        SendChatCommand = new AsyncRelayCommand(SendChatFromInputAsync, () => CanSendChat);
        GenerateNotesCommand = new AsyncRelayCommand(GenerateNotesAsync, () => CanGenerateNotes);
        AnnotateScreenCommand = new AsyncRelayCommand(AnnotateScreenAsync, () => CanAnnotateScreen);
    }

    public ObservableCollection<ChatTurn> ChatTurns { get; } = [];
    public ObservableCollection<string> SuggestedPrompts { get; } = [];
    public IReadOnlyList<string> ProviderOptions => DefaultProviderOptions;
    public IReadOnlyList<string> ModelOptions => DefaultModelOptions;
    public IReadOnlyList<string> GroqModelOptions => DefaultGroqModelOptions;
    public IReadOnlyList<string> ThinkingOptions => DefaultThinkingOptions;

    public AnalysisState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSendChat)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGenerateNotes)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAnnotateScreen)));
                (SendChatCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (GenerateNotesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (AnnotateScreenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
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

    public BitmapSource? CapturePreview
    {
        get => _capturePreview;
        private set
        {
            if (SetField(ref _capturePreview, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCapturePreview)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoCapturePreview)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGenerateNotes)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAnnotateScreen)));
                (GenerateNotesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (AnnotateScreenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasCapturePreview => CapturePreview is not null;
    public bool HasNoCapturePreview => CapturePreview is null;

    public BitmapSource? GeneratedNotesPreview
    {
        get => _generatedNotesPreview;
        private set
        {
            if (SetField(ref _generatedNotesPreview, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasGeneratedNotesPreview)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoGeneratedNotesPreview)));
            }
        }
    }

    public bool HasGeneratedNotesPreview => GeneratedNotesPreview is not null;
    public bool HasNoGeneratedNotesPreview => GeneratedNotesPreview is null;

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

    public string SelectedTextProvider
    {
        get => _selectedTextProvider;
        set
        {
            if (SetField(ref _selectedTextProvider, value))
            {
                RaiseSettingsChanged();
            }
        }
    }

    public string SelectedAnnotationProvider
    {
        get => _selectedAnnotationProvider;
        set
        {
            if (SetField(ref _selectedAnnotationProvider, value))
            {
                RaiseSettingsChanged();
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

    public string SelectedGroqModelId
    {
        get => _selectedGroqModelId;
        set
        {
            if (SetField(ref _selectedGroqModelId, value))
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

    public bool FocusSidebarAfterCapture
    {
        get => _focusSidebarAfterCapture;
        set
        {
            if (SetField(ref _focusSidebarAfterCapture, value))
            {
                RaiseSettingsChanged();
            }
        }
    }

    public string ChatInput
    {
        get => _chatInput;
        set
        {
            if (SetField(ref _chatInput, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSendChat)));
                (SendChatCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanSendChat =>
        !string.IsNullOrWhiteSpace(ChatInput)
        && _chatSendAction is not null
        && State is not AnalysisState.Uploading;

    public bool CanGenerateNotes =>
        HasCapturePreview
        && _generateNotesAction is not null
        && State is not AnalysisState.Capturing
        && State is not AnalysisState.Uploading
        && State is not AnalysisState.Rendering;

    public bool CanAnnotateScreen =>
        HasCapturePreview
        && _annotateScreenAction is not null
        && State is not AnalysisState.Capturing
        && State is not AnalysisState.Uploading
        && State is not AnalysisState.Rendering;

    public ICommand RetryCommand { get; }
    public ICommand SendChatCommand { get; }
    public ICommand GenerateNotesCommand { get; }
    public ICommand AnnotateScreenCommand { get; }

    public void SetRetryAction(Func<Task>? retryAction)
    {
        _retryAction = retryAction;
        CanRetry = retryAction is not null;
    }

    public void SetChatSendAction(Func<string, bool, Task>? sendAction)
    {
        _chatSendAction = sendAction;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSendChat)));
        (SendChatCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public void SetGenerateNotesAction(Func<Task>? generateNotesAction)
    {
        _generateNotesAction = generateNotesAction;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGenerateNotes)));
        (GenerateNotesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public void SetAnnotateScreenAction(Func<Task>? annotateScreenAction)
    {
        _annotateScreenAction = annotateScreenAction;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAnnotateScreen)));
        (AnnotateScreenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public void ApplySettings(AppSettings settings)
    {
        _suppressSettingsChanged = true;
        try
        {
            SelectedTextProvider = string.IsNullOrWhiteSpace(settings.TextProvider)
                ? "Gemini"
                : settings.TextProvider;
            SelectedAnnotationProvider = string.IsNullOrWhiteSpace(settings.AnnotationProvider)
                ? "Groq"
                : settings.AnnotationProvider;
            SelectedModelId = string.IsNullOrWhiteSpace(settings.ModelId)
                ? "gemini-3.1-pro-preview"
                : settings.ModelId;
            SelectedGroqModelId = string.IsNullOrWhiteSpace(settings.GroqModelId)
                ? "meta-llama/llama-4-scout-17b-16e-instruct"
                : settings.GroqModelId;
            SelectedThinkingLevel = string.IsNullOrWhiteSpace(settings.ThinkingLevel)
                ? "low"
                : settings.ThinkingLevel;
            AutoStartEnabled = settings.AutoStartEnabled;
            FocusSidebarAfterCapture = settings.FocusSidebarAfterCapture;
        }
        finally
        {
            _suppressSettingsChanged = false;
        }
    }

    public void SetSuggestedPrompts(IEnumerable<string>? prompts)
    {
        SuggestedPrompts.Clear();
        if (prompts is null)
        {
            return;
        }

        foreach (var prompt in prompts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(5))
        {
            SuggestedPrompts.Add(prompt.Trim());
        }
    }

    public void AddChatTurn(string prompt, string response)
    {
        ChatTurns.Add(new ChatTurn(DateTimeOffset.Now, prompt, response));
        while (ChatTurns.Count > 20)
        {
            ChatTurns.RemoveAt(0);
        }
    }

    public void ClearChatSession()
    {
        ChatTurns.Clear();
        SuggestedPrompts.Clear();
        ChatInput = string.Empty;
    }

    public void SetCapturePreview(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            CapturePreview = null;
            return;
        }

        using var stream = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        CapturePreview = bitmap;
    }

    public void SetGeneratedNotesPreview(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            GeneratedNotesPreview = null;
            return;
        }

        using var stream = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        GeneratedNotesPreview = bitmap;
    }

    public async Task SendSuggestedPromptAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || _chatSendAction is null)
        {
            return;
        }

        ChatInput = prompt.Trim();
        await SendChatAsync(isSuggestedPrompt: true).ConfigureAwait(true);
    }

    private async Task RetryAsync()
    {
        if (_retryAction is null)
        {
            return;
        }

        await _retryAction().ConfigureAwait(true);
    }

    private async Task SendChatFromInputAsync()
    {
        await SendChatAsync(isSuggestedPrompt: false).ConfigureAwait(true);
    }

    private async Task GenerateNotesAsync()
    {
        if (_generateNotesAction is null)
        {
            return;
        }

        await _generateNotesAction().ConfigureAwait(true);
    }

    private async Task AnnotateScreenAsync()
    {
        if (_annotateScreenAction is null)
        {
            return;
        }

        await _annotateScreenAction().ConfigureAwait(true);
    }

    private async Task SendChatAsync(bool isSuggestedPrompt)
    {
        var prompt = ChatInput.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || _chatSendAction is null)
        {
            return;
        }

        ChatInput = string.Empty;
        await _chatSendAction(prompt, isSuggestedPrompt).ConfigureAwait(true);
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
