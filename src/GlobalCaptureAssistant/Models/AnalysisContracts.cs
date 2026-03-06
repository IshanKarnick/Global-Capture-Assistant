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
    TimeSpan Latency,
    IReadOnlyList<string>? SuggestedPrompts = null);

public sealed record NoteCardHtmlResult(string Html, TimeSpan Latency);

public sealed record AnalysisHistoryItem(DateTimeOffset Timestamp, string Title, string Summary);
public sealed record ChatTurn(DateTimeOffset Timestamp, string Prompt, string Response);

public enum AnalysisState
{
    Idle,
    Capturing,
    Uploading,
    Rendering,
    Error
}
