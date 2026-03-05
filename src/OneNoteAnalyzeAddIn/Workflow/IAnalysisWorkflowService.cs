namespace OneNoteAnalyzeAddIn.Workflow;

public interface IAnalysisWorkflowService
{
    Task StartAnalysisFromCaptureAsync(CancellationToken cancellationToken = default);
    void SetStartupNotice(string? message);
}
