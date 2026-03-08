namespace GlobalCaptureAssistant.Models;

public sealed record ScreenAnnotationDocument(
    IReadOnlyList<ScreenAnnotationItem> Annotations);

/// <summary>
/// A single force entry used inside a <c>free_body_diagram</c> annotation panel.
/// </summary>
/// <param name="Label">Short force name, e.g. "F_g" or "N".</param>
/// <param name="Angle">Direction in degrees. 0 = right, 90 = down.</param>
/// <param name="Magnitude">Optional magnitude string, e.g. "9.8 N".</param>
/// <param name="Color">Optional CSS/WPF color string.</param>
public sealed record ForceEntry(string Label, double Angle, string? Magnitude, string? Color);

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
    string? Emphasis,
    /// <summary>Rotation in degrees (0 = right, 90 = down). Used by <c>force_vector</c>.</summary>
    double? Angle = null,
    /// <summary>Force entries for a <c>free_body_diagram</c> panel.</summary>
    IReadOnlyList<ForceEntry>? Forces = null,
    /// <summary>Magnitude label (e.g. "9.8 N"). Used by <c>force_vector</c>.</summary>
    string? Magnitude = null);

