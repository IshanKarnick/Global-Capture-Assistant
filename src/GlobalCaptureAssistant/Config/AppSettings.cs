namespace GlobalCaptureAssistant.Config;

public sealed class AppSettings
{
    public bool AutoStartEnabled { get; set; } = true;
    public bool FocusSidebarAfterCapture { get; set; } = true;
    public bool SidebarPinned { get; set; } = true;
    public bool ReducedMotion { get; set; } = false;
    public string ModelId { get; set; } = "gemini-3.1-pro-preview";
    public string ThinkingLevel { get; set; } = "low";
    public int RequestTimeoutSeconds { get; set; } = 45;
    public int MaxRetries { get; set; } = 2;
    public string? EncryptedApiKey { get; set; }
}
