using OneNoteAnalyzeAddIn.Analysis;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.Tests;

public sealed class GeminiPromptComposerTests
{
    [Fact]
    public void ComposePrompt_IncludesMetadata_WhenContextProvided()
    {
        var composer = new GeminiPromptComposer();
        var request = new AnalyzeRequest(
            [0x01, 0x02],
            new PageContext("page-1", "Sprint Plan", "Roadmap", "Engineering"),
            "Find action items.",
            "abc123");

        var prompt = composer.ComposePrompt(request);

        Assert.Contains("Notebook: Engineering", prompt);
        Assert.Contains("Section: Roadmap", prompt);
        Assert.Contains("Page title: Sprint Plan", prompt);
        Assert.Contains("Find action items.", prompt);
    }
}
