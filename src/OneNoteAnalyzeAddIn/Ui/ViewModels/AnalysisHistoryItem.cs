namespace OneNoteAnalyzeAddIn.Ui.ViewModels;

public sealed record AnalysisHistoryItem(
    DateTimeOffset Timestamp,
    string Title,
    string Summary);
