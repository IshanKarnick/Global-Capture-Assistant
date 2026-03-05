using System.Text;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.Analysis;

public sealed class GeminiPromptComposer
{
    public string ComposePrompt(AnalyzeRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Analyze this OneNote capture. Focus on actionable insights.");
        builder.AppendLine("Return concise bullets with key findings, assumptions, and next steps.");

        if (request.Context is not null)
        {
            builder.AppendLine();
            builder.AppendLine("OneNote context:");
            builder.AppendLine($"- Notebook: {request.Context.NotebookName}");
            builder.AppendLine($"- Section: {request.Context.SectionName}");
            builder.AppendLine($"- Page title: {request.Context.PageTitle}");
            builder.AppendLine($"- Page ID: {request.Context.PageId}");
        }

        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            builder.AppendLine();
            builder.AppendLine("User instruction:");
            builder.AppendLine(request.UserPrompt.Trim());
        }

        return builder.ToString();
    }
}
