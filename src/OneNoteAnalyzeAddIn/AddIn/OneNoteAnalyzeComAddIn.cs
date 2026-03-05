using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.IO;
using OneNoteAnalyzeAddIn.Analysis;
using OneNoteAnalyzeAddIn.Config;
using OneNoteAnalyzeAddIn.Capture;
using OneNoteAnalyzeAddIn.Diagnostics;
using OneNoteAnalyzeAddIn.Hotkeys;
using OneNoteAnalyzeAddIn.OneNote;
using OneNoteAnalyzeAddIn.Ui;
using OneNoteAnalyzeAddIn.Ui.ViewModels;
using OneNoteAnalyzeAddIn.Workflow;

namespace OneNoteAnalyzeAddIn.AddIn;

[ComVisible(true)]
[Guid("D3D78A72-7DF9-45BE-A7A6-588940F65B0A")]
[ProgId("OneNoteAnalyzeAddIn.Connect")]
public sealed class OneNoteAnalyzeComAddIn : IDTExtensibility2, IRibbonExtensibility
{
    private AppLogger? _logger;
    private GlobalHotkeyService? _hotkeyService;
    private IAnalysisWorkflowService? _workflowService;

    public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
    {
        _logger = new AppLogger();
        _logger.Info($"OnConnection mode={connectMode}");

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        var promptComposer = new GeminiPromptComposer();
        var geminiClient = new GeminiClient(settingsStore, settings, promptComposer, _logger);
        var contextProvider = new OneNoteContextProvider(application, _logger);
        var companionViewModel = new CompanionViewModel();
        var windowManager = new CompanionWindowManager(companionViewModel);
        var overlayService = new OverlayCaptureService();

        _workflowService = new AnalysisWorkflowService(
            windowManager,
            overlayService,
            contextProvider,
            geminiClient,
            settingsStore,
            settings,
            _logger);

        _hotkeyService = new GlobalHotkeyService(_logger);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        if (settings.HotkeyEnabled)
        {
            _hotkeyService.TryRegisterCtrlShiftQ();
        }
    }

    public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom)
    {
        _logger?.Info($"OnDisconnection mode={removeMode}");
        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.Dispose();
            _hotkeyService = null;
        }

        _workflowService = null;
    }

    public void OnAddInsUpdate(ref Array custom)
    {
    }

    public void OnStartupComplete(ref Array custom)
    {
    }

    public void OnBeginShutdown(ref Array custom)
    {
    }

    public string GetCustomUI(string ribbonId)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OneNoteAnalyzeAddIn.AddIn.Ribbon.xml");
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void OnAnalyzeSelectionClicked(IRibbonControl control)
    {
        BeginWorkflow();
    }

    public void OnAnalyzeSelectionClicked(object control)
    {
        BeginWorkflow();
    }

    public string GetAnalyzeSelectionLabel(IRibbonControl control)
    {
        return "Analyze Selection";
    }

    public string GetAnalyzeSelectionLabel(object control)
    {
        return "Analyze Selection";
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        BeginWorkflow();
    }

    private void BeginWorkflow()
    {
        if (_workflowService is null)
        {
            return;
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = dispatcher.InvokeAsync(async () => await _workflowService.StartAnalysisFromCaptureAsync().ConfigureAwait(true));
    }
}
