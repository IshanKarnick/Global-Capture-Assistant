using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using GlobalCaptureAssistant.Analysis;
using GlobalCaptureAssistant.Capture;
using GlobalCaptureAssistant.Config;
using GlobalCaptureAssistant.Diagnostics;
using GlobalCaptureAssistant.Models;
using GlobalCaptureAssistant.Platform;
using GlobalCaptureAssistant.Ui;
using GlobalCaptureAssistant.Ui.ViewModels;

namespace GlobalCaptureAssistant.Runtime;

public sealed class AppHost : IDisposable
{
    private readonly AppLogger _logger = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly OverlayCaptureService _overlayCaptureService = new();
    private readonly ActiveWindowService _activeWindowService = new();
    private readonly AutoStartService _autoStartService = new();
    private readonly TrayIconService _trayIconService = new();
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly SidebarViewModel _sidebarViewModel = new();
    private readonly NoteCardRenderService _noteCardRenderService = new();

    private AppSettings _settings;
    private GeminiClient _geminiClient;
    private GroqClient _groqClient;
    private FloatingButtonWindow? _floatingButton;
    private SidebarWindow? _sidebarWindow;
    private AnalyzeRequest? _lastRequest;

    public AppHost()
    {
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        _groqClient = new GroqClient(_settingsStore, _settings, _logger);
        _sidebarViewModel.ApplySettings(_settings);
        _sidebarViewModel.SettingsChanged += OnSidebarSettingsChanged;
        _sidebarViewModel.SetChatSendAction(ChatMoreAsync);
        _sidebarViewModel.SetGenerateNotesAction(GenerateNotesCardAsync);
        _sidebarViewModel.SetAnnotateScreenAction(AnnotateScreenAsync);
    }

    public void Start()
    {
        _logger.Info("Starting GlobalCaptureAssistant host.");

        _autoStartService.SetEnabled(_settings.AutoStartEnabled);

        _floatingButton = new FloatingButtonWindow();
        _floatingButton.CaptureRequested += (_, _) => _ = CaptureAndAnalyzeAsync(useLastCapture: false);
        _floatingButton.Show();

        _sidebarWindow = new SidebarWindow(_sidebarViewModel);

        _trayIconService.CaptureRequested += (_, _) => _ = CaptureAndAnalyzeAsync(useLastCapture: false);
        _trayIconService.ShowSidebarRequested += (_, _) => ShowSidebar(activate: true);
        _trayIconService.ExitRequested += (_, _) => System.Windows.Application.Current.Shutdown();

        _hotkeyService.HotkeyPressed += (_, _) => _ = CaptureAndAnalyzeAsync(useLastCapture: false);
        if (!_hotkeyService.RegisterCtrlShiftQ())
        {
            _sidebarViewModel.StatusText = "Hotkey unavailable. Use floating button or tray.";
        }
    }

    private async Task CaptureAndAnalyzeAsync(bool useLastCapture)
    {
        try
        {
            ShowSidebar(activate: false);
            _sidebarViewModel.SetRetryAction(() => CaptureAndAnalyzeAsync(useLastCapture: true));
            if (!EnsureTextProviderApiKey())
            {
                _sidebarViewModel.StatusText = $"{GetTextProviderName()} API key is required.";
                return;
            }

            AnalyzeRequest? request = useLastCapture ? _lastRequest : null;
            if (request is null)
            {
                _sidebarViewModel.State = AnalysisState.Capturing;
                _sidebarViewModel.StatusText = "Select an area to capture";
                _sidebarViewModel.ErrorText = string.Empty;
                _sidebarViewModel.CanRetry = false;

                var floatingWasVisible = _floatingButton?.IsVisible == true;
                var sidebarWasVisible = _sidebarWindow?.IsVisible == true;
                if (floatingWasVisible)
                {
                    _floatingButton?.Hide();
                }
                if (sidebarWasVisible)
                {
                    _sidebarWindow?.Hide();
                }

                CaptureResult capture;
                try
                {
                    capture = await _overlayCaptureService.CaptureAsync(CancellationToken.None).ConfigureAwait(true);
                }
                finally
                {
                    if (floatingWasVisible)
                    {
                        _floatingButton?.Show();
                    }
                    if (sidebarWasVisible)
                    {
                        ShowSidebar(activate: false);
                    }
                }

                if (capture.IsCanceled || capture.PngBytes is null)
                {
                    _sidebarViewModel.StatusText = "Capture canceled";
                    _sidebarViewModel.State = AnalysisState.Idle;
                    return;
                }

                var windowContext = _activeWindowService.TryGetContext();
                _sidebarViewModel.SetCapturePreview(capture.PngBytes);
                _sidebarViewModel.SetGeneratedNotesPreview(null);
                _sidebarViewModel.ContextText = windowContext is null
                    ? "No active-window context available."
                    : $"{windowContext.ProcessName} / {windowContext.Title}";

                request = new AnalyzeRequest(
                    capture.PngBytes,
                    windowContext,
                    string.Empty,
                    Guid.NewGuid().ToString("N")[..8]);

                _lastRequest = request;
                _sidebarViewModel.ClearChatSession();
            }

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = $"Analyzing with {GetTextProviderName()}...";
            var response = await AnalyzeWithSelectedTextProviderAsync(request).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Generating follow-up prompts...";
            var suggestions = await GenerateSuggestedPromptsAsync(response.Text).ConfigureAwait(true);

            _sidebarViewModel.ResultText = response.Text;
            _sidebarViewModel.SetSuggestedPrompts(suggestions);
            _sidebarViewModel.AddChatTurn("Initial image analysis", response.Text);
            _sidebarViewModel.ErrorText = string.Empty;
            _sidebarViewModel.CanRetry = false;
            _sidebarViewModel.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.State = AnalysisState.Idle;
            _sidebarWindow?.ScrollToTop();
            if (_settings.FocusSidebarAfterCapture)
            {
                ShowSidebar(activate: true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Capture/analysis workflow failed.", ex);
            _sidebarViewModel.State = AnalysisState.Error;
            _sidebarViewModel.StatusText = "Analysis failed";
            _sidebarViewModel.ErrorText = ex.Message;
            _sidebarViewModel.CanRetry = _lastRequest is not null;
        }
    }

    private async Task ChatMoreAsync(string prompt, bool scrollToTopOnComplete)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        if (_lastRequest is null)
        {
            _sidebarViewModel.StatusText = "Capture an image first.";
            return;
        }

        try
        {
            ShowSidebar(activate: true);
            if (!EnsureTextProviderApiKey())
            {
                _sidebarViewModel.StatusText = $"{GetTextProviderName()} API key is required.";
                return;
            }

            var followUpRequest = _lastRequest with
            {
                UserPrompt = prompt.Trim(),
                CorrelationId = Guid.NewGuid().ToString("N")[..8]
            };

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = $"Analyzing follow-up with {GetTextProviderName()}...";
            _sidebarViewModel.ErrorText = string.Empty;

            var response = await AnalyzeWithSelectedTextProviderAsync(followUpRequest).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Generating follow-up prompts...";
            var suggestions = await GenerateSuggestedPromptsAsync(response.Text).ConfigureAwait(true);

            _lastRequest = followUpRequest;
            _sidebarViewModel.ResultText = response.Text;
            _sidebarViewModel.SetSuggestedPrompts(suggestions);
            _sidebarViewModel.AddChatTurn(prompt.Trim(), response.Text);
            _sidebarViewModel.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.State = AnalysisState.Idle;
            _sidebarViewModel.CanRetry = false;
            if (scrollToTopOnComplete)
            {
                _sidebarWindow?.ScrollToTop();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Chat more workflow failed.", ex);
            _sidebarViewModel.State = AnalysisState.Error;
            _sidebarViewModel.StatusText = "Chat failed";
            _sidebarViewModel.ErrorText = ex.Message;
            _sidebarViewModel.CanRetry = _lastRequest is not null;
        }
    }

    private async Task GenerateNotesCardAsync()
    {
        if (_lastRequest is null || _lastRequest.ImagePng is null || _lastRequest.ImagePng.Length == 0)
        {
            _sidebarViewModel.StatusText = "Capture an image first.";
            return;
        }

        try
        {
            ShowSidebar(activate: true);
            _sidebarViewModel.SetRetryAction(GenerateNotesCardAsync);
            if (!EnsureApiKey())
            {
                _sidebarViewModel.StatusText = "Gemini API key is required.";
                return;
            }

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = "Generating notes card...";
            _sidebarViewModel.ErrorText = string.Empty;

            var noteCardHtml = await _geminiClient.GenerateNotesCardHtmlAsync(_lastRequest, CancellationToken.None).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Rendering notes card...";

            var pngBytes = await _noteCardRenderService.RenderHtmlToPngAsync(noteCardHtml.Html, CancellationToken.None).ConfigureAwait(true);
            CopyPngToClipboard(pngBytes);
            _sidebarViewModel.SetGeneratedNotesPreview(pngBytes);

            _sidebarViewModel.ErrorText = string.Empty;
            _sidebarViewModel.CanRetry = false;
            _sidebarViewModel.StatusText = $"Notes card copied in {noteCardHtml.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.State = AnalysisState.Idle;
            _sidebarWindow?.ScrollToTop();
        }
        catch (Exception ex)
        {
            _logger.Error("Generate notes card workflow failed.", ex);
            _sidebarViewModel.State = AnalysisState.Error;
            _sidebarViewModel.StatusText = "Notes card failed";
            _sidebarViewModel.ErrorText = ex.Message;
            _sidebarViewModel.CanRetry = _lastRequest is not null;
        }
    }

    private async Task AnnotateScreenAsync()
    {
        if (_lastRequest is null || _lastRequest.ImagePng.Length == 0)
        {
            _sidebarViewModel.StatusText = "Capture an image first.";
            return;
        }

        try
        {
            ShowSidebar(activate: true);
            _sidebarViewModel.SetRetryAction(AnnotateScreenAsync);
            if (!EnsureAnnotationProviderApiKey())
            {
                _sidebarViewModel.StatusText = $"{GetAnnotationProviderName()} API key is required.";
                return;
            }

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = $"Annotating with {GetAnnotationProviderName()}...";
            _sidebarViewModel.ErrorText = string.Empty;

            var document = await GenerateAnnotationsAsync(_lastRequest).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Opening annotation overlay...";

            var screenshot = CreateBitmapSource(_lastRequest.ImagePng);
            var overlayWindow = new AnnotatedScreenWindow(screenshot, document)
            {
                Owner = _sidebarWindow
            };
            overlayWindow.Show();

            _sidebarViewModel.ErrorText = string.Empty;
            _sidebarViewModel.CanRetry = false;
            _sidebarViewModel.StatusText = "Annotation overlay ready";
            _sidebarViewModel.State = AnalysisState.Idle;
        }
        catch (Exception ex)
        {
            _logger.Error("Screen annotation workflow failed.", ex);
            _sidebarViewModel.State = AnalysisState.Error;
            _sidebarViewModel.StatusText = "Annotation failed";
            _sidebarViewModel.ErrorText = ex.Message;
            _sidebarViewModel.CanRetry = _lastRequest is not null;
        }
    }

    private void ShowSidebar(bool activate)
    {
        if (_sidebarWindow is null)
        {
            _sidebarWindow = new SidebarWindow(_sidebarViewModel);
        }

        _sidebarWindow.ShowActivated = activate;
        if (_sidebarWindow.WindowState == WindowState.Minimized)
        {
            _sidebarWindow.WindowState = WindowState.Normal;
        }

        _sidebarWindow.Show();
        if (activate)
        {
            _sidebarWindow.Activate();
        }
    }

    private bool EnsureApiKey(string subtitle = "Required to use Gemini-powered features")
    {
        if (!string.IsNullOrWhiteSpace(_settingsStore.GetApiKey(_settings)))
        {
            return true;
        }

        var prompt = new ApiKeyPromptWindow("Gemini API Key", subtitle);
        var response = prompt.ShowDialog();
        if (response != true || string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            return false;
        }

        _settingsStore.SetApiKey(_settings, prompt.ApiKey);
        _settingsStore.Save(_settings);
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        _groqClient = new GroqClient(_settingsStore, _settings, _logger);
        return true;
    }

    private bool EnsureGroqApiKey(string subtitle = "Required to use Groq-powered features")
    {
        if (!string.IsNullOrWhiteSpace(_settingsStore.GetGroqApiKey(_settings)))
        {
            return true;
        }

        var prompt = new ApiKeyPromptWindow("Groq API Key", subtitle);
        var response = prompt.ShowDialog();
        if (response != true || string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            return false;
        }

        _settingsStore.SetGroqApiKey(_settings, prompt.ApiKey);
        _settingsStore.Save(_settings);
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        _groqClient = new GroqClient(_settingsStore, _settings, _logger);
        return true;
    }

    public void Dispose()
    {
        _sidebarViewModel.SettingsChanged -= OnSidebarSettingsChanged;
        _sidebarViewModel.SetChatSendAction(null);
        _sidebarViewModel.SetGenerateNotesAction(null);
        _sidebarViewModel.SetAnnotateScreenAction(null);
        _hotkeyService.Dispose();
        _trayIconService.Dispose();
        _noteCardRenderService.Dispose();
        _floatingButton?.Close();
        if (_sidebarWindow is not null)
        {
            _sidebarWindow.AllowClose();
            _sidebarWindow.Close();
        }
    }

    private void OnSidebarSettingsChanged(object? sender, EventArgs e)
    {
        _settings.TextProvider = _sidebarViewModel.SelectedTextProvider;
        _settings.AnnotationProvider = _sidebarViewModel.SelectedAnnotationProvider;
        _settings.ModelId = _sidebarViewModel.SelectedModelId;
        _settings.GroqModelId = _sidebarViewModel.SelectedGroqModelId;
        _settings.ThinkingLevel = _sidebarViewModel.SelectedThinkingLevel;
        _settings.AutoStartEnabled = _sidebarViewModel.AutoStartEnabled;
        _settings.FocusSidebarAfterCapture = _sidebarViewModel.FocusSidebarAfterCapture;

        _settingsStore.Save(_settings);
        _autoStartService.SetEnabled(_settings.AutoStartEnabled);
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        _groqClient = new GroqClient(_settingsStore, _settings, _logger);
    }

    private string GetTextProviderName() => NormalizeProvider(_settings.TextProvider, "Gemini");

    private string GetAnnotationProviderName() => NormalizeProvider(_settings.AnnotationProvider, "Groq");

    private bool EnsureTextProviderApiKey()
    {
        return IsGroqProvider(_settings.TextProvider)
            ? EnsureGroqApiKey("Required to use Groq for screenshot text analysis")
            : EnsureApiKey("Required to use Gemini for screenshot text analysis");
    }

    private bool EnsureAnnotationProviderApiKey()
    {
        return IsGroqProvider(_settings.AnnotationProvider)
            ? EnsureGroqApiKey("Required to generate Groq-based on-screen annotations")
            : EnsureApiKey("Required to generate Gemini-based on-screen annotations");
    }

    private Task<AnalyzeResponse> AnalyzeWithSelectedTextProviderAsync(AnalyzeRequest request)
    {
        return IsGroqProvider(_settings.TextProvider)
            ? _groqClient.AnalyzeImageAsync(request, CancellationToken.None)
            : _geminiClient.AnalyzeImageAsync(request, CancellationToken.None);
    }

    private Task<IReadOnlyList<string>> GenerateSuggestedPromptsAsync(string answer)
    {
        return IsGroqProvider(_settings.TextProvider)
            ? _groqClient.GenerateSuggestedPromptsAsync(answer, CancellationToken.None)
            : _geminiClient.GenerateSuggestedPromptsAsync(answer, CancellationToken.None);
    }

    private Task<ScreenAnnotationDocument> GenerateAnnotationsAsync(AnalyzeRequest request)
    {
        return IsGroqProvider(_settings.AnnotationProvider)
            ? _groqClient.GenerateAnnotationsAsync(request, CancellationToken.None)
            : _geminiClient.GenerateAnnotationsAsync(request, CancellationToken.None);
    }

    private static bool IsGroqProvider(string? provider)
    {
        return string.Equals(provider?.Trim(), "Groq", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProvider(string? provider, string fallback)
    {
        if (string.Equals(provider?.Trim(), "Groq", StringComparison.OrdinalIgnoreCase))
        {
            return "Groq";
        }

        if (string.Equals(provider?.Trim(), "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        return fallback;
    }

    private static void CopyPngToClipboard(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        System.Windows.Clipboard.SetImage(frame);
    }

    private static BitmapSource CreateBitmapSource(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
