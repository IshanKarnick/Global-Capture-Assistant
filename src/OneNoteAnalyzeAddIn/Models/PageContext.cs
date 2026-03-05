namespace OneNoteAnalyzeAddIn.Models;

public sealed record PageContext(
    string PageId,
    string PageTitle,
    string SectionName,
    string NotebookName);
