namespace GlobalCaptureAssistant.Models;

public sealed record ScreenAnnotationDocument(
    IReadOnlyList<ScreenAnnotationItem> Annotations);

public sealed record ScreenAnnotationItem(
    string Type,
    double X,
    double Y,
    double Width,
    double Height,
    double? EndX,
    double? EndY,
    string? Title,
    string? Text,
    string? Latex,
    string? Color,
    string? Emphasis);
