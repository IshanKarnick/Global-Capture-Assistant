using System.Text;
using GlobalCaptureAssistant.Models;

namespace GlobalCaptureAssistant.Analysis;

public sealed class GeminiPromptComposer
{
    public string ComposePrompt(AnalyzeRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Analyze this screenshot from the user's desktop.");
        builder.AppendLine("Respond with concise bullets: observations, likely intent, and recommended next actions.");

        if (request.WindowContext is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Foreground app context:");
            builder.AppendLine($"- Window title: {request.WindowContext.Title}");
            builder.AppendLine($"- Process: {request.WindowContext.ProcessName}");
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
