namespace GlobalCaptureAssistant.Config;

public sealed class AppSettings
{
    public bool AutoStartEnabled { get; set; } = true;
    public bool FocusSidebarAfterCapture { get; set; } = true;
    public bool SidebarPinned { get; set; } = true;
    public bool ReducedMotion { get; set; } = false;
    public string TextProvider { get; set; } = "Gemini";
    public string AnnotationProvider { get; set; } = "Groq";
    public string ModelId { get; set; } = "gemini-3.1-pro-preview";
    public string GroqModelId { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
    public string ThinkingLevel { get; set; } = "low";
    public int RequestTimeoutSeconds { get; set; } = 45;
    public int MaxRetries { get; set; } = 2;
    public string? EncryptedApiKey { get; set; }
    public string? EncryptedGroqApiKey { get; set; }
}
