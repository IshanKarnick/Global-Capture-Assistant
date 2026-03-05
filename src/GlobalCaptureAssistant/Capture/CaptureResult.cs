namespace GlobalCaptureAssistant.Capture;

public sealed record CaptureResult(bool IsCanceled, byte[]? PngBytes);
