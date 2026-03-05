namespace GlobalCaptureAssistant.Models;

public sealed record ActiveWindowContext(string Title, string ProcessName);

public sealed record AnalyzeRequest(
    byte[] ImagePng,
    ActiveWindowContext? WindowContext,
    string UserPrompt,
    string CorrelationId);

public sealed record AnalyzeResponse(
    string Text,
    int? InputTokens,
    int? OutputTokens,
    TimeSpan Latency);

public sealed record AnalysisHistoryItem(DateTimeOffset Timestamp, string Title, string Summary);

public enum AnalysisState
{
    Idle,
    Capturing,
    Uploading,
    Rendering,
    Error
}
