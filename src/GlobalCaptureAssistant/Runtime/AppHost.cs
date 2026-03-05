using System.Windows;
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

    private AppSettings _settings;
    private GeminiClient _geminiClient;
    private FloatingButtonWindow? _floatingButton;
    private SidebarWindow? _sidebarWindow;
    private AnalyzeRequest? _lastRequest;

    public AppHost()
    {
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        _sidebarViewModel.ApplySettings(_settings);
        _sidebarViewModel.SettingsChanged += OnSidebarSettingsChanged;
        _sidebarViewModel.SetChatSendAction(ChatMoreAsync);
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
        _trayIconService.ShowSidebarRequested += (_, _) => ShowSidebar();
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
            ShowSidebar();
            _sidebarViewModel.SetRetryAction(() => CaptureAndAnalyzeAsync(useLastCapture: true));
            if (!EnsureApiKey())
            {
                _sidebarViewModel.StatusText = "Gemini API key is required.";
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
                if (floatingWasVisible)
                {
                    _floatingButton?.Hide();
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
                }

                if (capture.IsCanceled || capture.PngBytes is null)
                {
                    _sidebarViewModel.StatusText = "Capture canceled";
                    _sidebarViewModel.State = AnalysisState.Idle;
                    return;
                }

                var windowContext = _activeWindowService.TryGetContext();
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
            _sidebarViewModel.StatusText = "Analyzing with Gemini...";
            var response = await _geminiClient.AnalyzeImageAsync(request, CancellationToken.None).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Generating follow-up prompts...";
            var suggestions = await _geminiClient.GenerateSuggestedPromptsAsync(response.Text, CancellationToken.None).ConfigureAwait(true);

            _sidebarViewModel.ResultText = response.Text;
            _sidebarViewModel.SetSuggestedPrompts(suggestions);
            _sidebarViewModel.AddChatTurn("Initial image analysis", response.Text);
            _sidebarViewModel.ErrorText = string.Empty;
            _sidebarViewModel.CanRetry = false;
            _sidebarViewModel.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.State = AnalysisState.Idle;
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

    private async Task ChatMoreAsync(string prompt)
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
            ShowSidebar();
            if (!EnsureApiKey())
            {
                _sidebarViewModel.StatusText = "Gemini API key is required.";
                return;
            }

            var followUpRequest = _lastRequest with
            {
                UserPrompt = prompt.Trim(),
                CorrelationId = Guid.NewGuid().ToString("N")[..8]
            };

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = "Analyzing follow-up...";
            _sidebarViewModel.ErrorText = string.Empty;

            var response = await _geminiClient.AnalyzeImageAsync(followUpRequest, CancellationToken.None).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = "Generating follow-up prompts...";
            var suggestions = await _geminiClient.GenerateSuggestedPromptsAsync(response.Text, CancellationToken.None).ConfigureAwait(true);

            _lastRequest = followUpRequest;
            _sidebarViewModel.ResultText = response.Text;
            _sidebarViewModel.SetSuggestedPrompts(suggestions);
            _sidebarViewModel.AddChatTurn(prompt.Trim(), response.Text);
            _sidebarViewModel.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.State = AnalysisState.Idle;
            _sidebarViewModel.CanRetry = false;
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

    private void ShowSidebar()
    {
        if (_sidebarWindow is null)
        {
            _sidebarWindow = new SidebarWindow(_sidebarViewModel);
        }

        _sidebarWindow.Show();
        _sidebarWindow.Activate();
    }

    private bool EnsureApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_settingsStore.GetApiKey(_settings)))
        {
            return true;
        }

        var prompt = new ApiKeyPromptWindow();
        var response = prompt.ShowDialog();
        if (response != true || string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            return false;
        }

        _settingsStore.SetApiKey(_settings, prompt.ApiKey);
        _settingsStore.Save(_settings);
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
        return true;
    }

    public void Dispose()
    {
        _sidebarViewModel.SettingsChanged -= OnSidebarSettingsChanged;
        _sidebarViewModel.SetChatSendAction(null);
        _hotkeyService.Dispose();
        _trayIconService.Dispose();
        _floatingButton?.Close();
        if (_sidebarWindow is not null)
        {
            _sidebarWindow.AllowClose();
            _sidebarWindow.Close();
        }
    }

    private void OnSidebarSettingsChanged(object? sender, EventArgs e)
    {
        _settings.ModelId = _sidebarViewModel.SelectedModelId;
        _settings.ThinkingLevel = _sidebarViewModel.SelectedThinkingLevel;
        _settings.AutoStartEnabled = _sidebarViewModel.AutoStartEnabled;

        _settingsStore.Save(_settings);
        _autoStartService.SetEnabled(_settings.AutoStartEnabled);
        _geminiClient = new GeminiClient(_settingsStore, _settings, _logger);
    }
}
