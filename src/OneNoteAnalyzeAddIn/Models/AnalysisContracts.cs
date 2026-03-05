namespace OneNoteAnalyzeAddIn.Models;

public sealed record AnalyzeRequest(
    byte[] ImagePng,
    PageContext? Context,
    string UserPrompt,
    string CorrelationId);

public sealed record AnalyzeResponse(
    string Text,
    int? InputTokens,
    int? OutputTokens,
    TimeSpan Latency);
