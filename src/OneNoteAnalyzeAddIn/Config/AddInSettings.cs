namespace OneNoteAnalyzeAddIn.Config;

public sealed class AddInSettings
{
    public bool IncludeMetadata { get; set; } = true;
    public bool HotkeyEnabled { get; set; } = true;
    public string HotkeyModifiers { get; set; } = "Ctrl+Shift";
    public string HotkeyKey { get; set; } = "Q";
    public string ModelId { get; set; } = "gemini-3.1-pro-preview";
    public string ThinkingLevel { get; set; } = "low";
    public int RequestTimeoutSeconds { get; set; } = 45;
    public int MaxRetries { get; set; } = 2;
    public string? EncryptedApiKey { get; set; }
}
