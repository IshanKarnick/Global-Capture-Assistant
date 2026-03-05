using OneNoteAnalyzeAddIn.Analysis;
using OneNoteAnalyzeAddIn.Config;
using OneNoteAnalyzeAddIn.Capture;
using OneNoteAnalyzeAddIn.Diagnostics;
using OneNoteAnalyzeAddIn.Models;
using OneNoteAnalyzeAddIn.OneNote;
using OneNoteAnalyzeAddIn.Ui;
using OneNoteAnalyzeAddIn.Ui.ViewModels;

namespace OneNoteAnalyzeAddIn.Workflow;

public sealed class AnalysisWorkflowService : IAnalysisWorkflowService
{
    private readonly CompanionWindowManager _windowManager;
    private readonly IOverlayCaptureService _overlayCaptureService;
    private readonly OneNoteContextProvider _oneNoteContextProvider;
    private readonly GeminiClient _geminiClient;
    private readonly AddInSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private AnalyzeRequest? _lastAnalyzeRequest;
    private string _lastCaptureTitle = "Captured region";

    public AnalysisWorkflowService(
        CompanionWindowManager windowManager,
        IOverlayCaptureService overlayCaptureService,
        OneNoteContextProvider oneNoteContextProvider,
        GeminiClient geminiClient,
        SettingsStore settingsStore,
        AddInSettings settings,
        AppLogger logger)
    {
        _windowManager = windowManager;
        _overlayCaptureService = overlayCaptureService;
        _oneNoteContextProvider = oneNoteContextProvider;
        _geminiClient = geminiClient;
        _settingsStore = settingsStore;
        _settings = settings;
        _logger = logger;
    }

    public Task StartAnalysisFromCaptureAsync(CancellationToken cancellationToken = default)
        => ExecuteWorkflowAsync(reuseLastCapture: false, cancellationToken);

    public void SetStartupNotice(string? message)
    {
        _windowManager.ViewModel.NoticeText = message ?? string.Empty;
    }

    private async Task ExecuteWorkflowAsync(bool reuseLastCapture, CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            _windowManager.Show();
            var vm = _windowManager.ViewModel;
            vm.SetRetryAction(() => ExecuteWorkflowAsync(reuseLastCapture: true, CancellationToken.None));

            if (!EnsureApiKey(vm))
            {
                vm.SetRetryAction(() => ExecuteWorkflowAsync(reuseLastCapture: false, CancellationToken.None));
                return;
            }

            AnalyzeRequest? request = reuseLastCapture ? _lastAnalyzeRequest : null;
            if (request is null)
            {
                vm.State = AnalysisViewState.Capturing;
                vm.StatusText = "Select an area to analyze";
                vm.ErrorText = string.Empty;
                vm.CanRetry = false;

                var capture = await _overlayCaptureService.CaptureRegionAsync(cancellationToken).ConfigureAwait(true);
                if (capture.IsCanceled || capture.PngBytes is null)
                {
                    vm.State = AnalysisViewState.Idle;
                    vm.StatusText = "Capture canceled";
                    return;
                }

                var correlationId = Guid.NewGuid().ToString("N")[..8];
                var pageContext = _settings.IncludeMetadata ? _oneNoteContextProvider.TryGetActivePageContext(correlationId) : null;
                vm.MetadataText = pageContext is null
                    ? "No page metadata available."
                    : $"{pageContext.NotebookName} / {pageContext.SectionName} / {pageContext.PageTitle} / {pageContext.PageId}";

                _lastCaptureTitle = pageContext?.PageTitle ?? "Captured region";
                request = new AnalyzeRequest(
                    capture.PngBytes,
                    pageContext,
                    "Summarize what is shown and suggest next actions.",
                    correlationId);
                _lastAnalyzeRequest = request;
            }
            else
            {
                vm.StatusText = "Retrying last capture...";
            }

            vm.State = AnalysisViewState.Uploading;
            vm.StatusText = "Analyzing with Gemini...";
            var response = await _geminiClient.AnalyzeImageAsync(request, cancellationToken).ConfigureAwait(true);

            vm.State = AnalysisViewState.Rendering;
            vm.StatusText = $"Done in {response.Latency.TotalSeconds:F1}s";
            vm.ResultText = response.Text;
            vm.ErrorText = string.Empty;
            vm.CanRetry = false;
            vm.AppendHistory(_lastCaptureTitle, response.Text.Length > 120 ? $"{response.Text[..120]}..." : response.Text);
            vm.State = AnalysisViewState.Idle;
        }
        catch (Exception ex)
        {
            var vm = _windowManager.ViewModel;
            vm.State = AnalysisViewState.Error;
            vm.StatusText = "Analysis failed";
            vm.ErrorText = ex.Message;
            vm.CanRetry = _lastAnalyzeRequest is not null;
            _logger.Error("Analysis workflow failed.", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool EnsureApiKey(CompanionViewModel vm)
    {
        var existing = _settingsStore.GetApiKey(_settings);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return true;
        }

        var prompt = new ApiKeyPromptWindow();
        var response = prompt.ShowDialog();
        if (response != true || string.IsNullOrWhiteSpace(prompt.ApiKey))
        {
            vm.State = AnalysisViewState.Idle;
            vm.StatusText = "Gemini API key is required";
            vm.ResultText = "Set a Gemini API key to continue.";
            return false;
        }

        _settingsStore.SetApiKey(_settings, prompt.ApiKey);
        _settingsStore.Save(_settings);
        return true;
    }
}
