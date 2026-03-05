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
    private string _lastTitle = "Captured region";

    public AppHost()
    {
        _settings = _settingsStore.Load();
        _geminiClient = new GeminiClient(_settingsStore, _settings, new GeminiPromptComposer(), _logger);
        _sidebarViewModel.ApplySettings(_settings);
        _sidebarViewModel.SettingsChanged += OnSidebarSettingsChanged;
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
                    "Summarize what is shown and suggest practical next actions.",
                    Guid.NewGuid().ToString("N")[..8]);
                _lastRequest = request;
                _lastTitle = windowContext?.Title ?? "Captured region";
            }

            _sidebarViewModel.State = AnalysisState.Uploading;
            _sidebarViewModel.StatusText = "Analyzing with Gemini...";
            var response = await _geminiClient.AnalyzeImageAsync(request, CancellationToken.None).ConfigureAwait(true);

            _sidebarViewModel.State = AnalysisState.Rendering;
            _sidebarViewModel.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            _sidebarViewModel.ResultText = response.Text;
            _sidebarViewModel.ErrorText = string.Empty;
            _sidebarViewModel.CanRetry = false;
            _sidebarViewModel.AddHistory(_lastTitle, response.Text.Length > 120 ? $"{response.Text[..120]}..." : response.Text);
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
        _geminiClient = new GeminiClient(_settingsStore, _settings, new GeminiPromptComposer(), _logger);
        return true;
    }

    public void Dispose()
    {
        _sidebarViewModel.SettingsChanged -= OnSidebarSettingsChanged;
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
        _geminiClient = new GeminiClient(_settingsStore, _settings, new GeminiPromptComposer(), _logger);
    }
}
