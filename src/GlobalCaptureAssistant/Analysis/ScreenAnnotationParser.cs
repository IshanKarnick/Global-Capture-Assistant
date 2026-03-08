using System.Globalization;
using System.Text.Json;
using GlobalCaptureAssistant.Models;

namespace GlobalCaptureAssistant.Analysis;

internal static class ScreenAnnotationParser
{
    public static ScreenAnnotationDocument Parse(string payload, string providerName)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException($"{providerName} returned an empty annotation payload.");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var annotationsElement = ResolveAnnotationsElement(root);
        if (annotationsElement is null || annotationsElement.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{providerName} annotation payload is missing the annotations array.");
        }

        var annotations = new List<ScreenAnnotationItem>();
        foreach (var item in annotationsElement.Value.EnumerateArray())
        {
            var bounds = ReadBounds(item);
            annotations.Add(new ScreenAnnotationItem(
                NormalizeType(ReadFirstFlexibleString(item, "type", "kind", "annotation_type") ?? "label"),
                ReadFirstDouble(item, bounds, "x", "left", "x1"),
                ReadFirstDouble(item, bounds, "y", "top", "y1"),
                ReadWidth(item, bounds),
                ReadHeight(item, bounds),
                ReadFirstNullableDouble(item, "endX", "end_x", "toX", "targetX", "x2", "right"),
                ReadFirstNullableDouble(item, "endY", "end_y", "toY", "targetY", "y2", "bottom"),
                ReadFirstFlexibleString(item, "title", "heading", "header"),
                ReadFirstFlexibleString(item, "text", "label", "description", "content", "note", "explanation", "summary"),
                ReadFirstFlexibleString(item, "latex", "equation", "math"),
                ReadFirstFlexibleString(item, "color", "strokeColor", "accentColor"),
                ReadFirstFlexibleString(item, "emphasis", "style", "severity"),
                Angle: ReadFirstNullableDouble(item, "angle", "direction_degrees", "direction"),
                Forces: ReadForces(item),
                Magnitude: ReadFirstFlexibleString(item, "magnitude", "value", "mag")));
        }

        if (annotations.Count == 0)
        {
            throw new InvalidOperationException($"{providerName} returned no annotations.");
        }

        return new ScreenAnnotationDocument(annotations);
    }

    private static JsonElement? ResolveAnnotationsElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("annotations", out var annotations))
        {
            return annotations;
        }

        if (root.TryGetProperty("items", out var items))
        {
            return items;
        }

        if (root.TryGetProperty("callouts", out var callouts))
        {
            return callouts;
        }

        if (root.TryGetProperty("regions", out var regions))
        {
            return regions;
        }

        return null;
    }

    private static string? ReadFirstFlexibleString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString(),
                    JsonValueKind.Number => property.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Object => property.GetRawText(),
                    JsonValueKind.Array => property.GetRawText(),
                    _ => null
                };
            }
        }

        return null;
    }

    private static double ReadFirstDouble(JsonElement element, AnnotationBounds? bounds, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryReadDouble(element, propertyName, out var value))
            {
                return value;
            }
        }

        return propertyNames.Contains("x") || propertyNames.Contains("left") || propertyNames.Contains("x1")
            ? bounds?.X ?? 0
            : bounds?.Y ?? 0;
    }

    private static double? ReadFirstNullableDouble(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryReadDouble(element, propertyName, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryReadDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.Number:
                value = property.GetDouble();
                return true;
            case JsonValueKind.String:
                var raw = property.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                raw = raw.Trim().TrimEnd('%');
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    if (property.GetString()!.Contains('%'))
                    {
                        value /= 100d;
                    }

                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static AnnotationBounds? ReadBounds(JsonElement element)
    {
        if (element.TryGetProperty("bbox", out var bbox) && bbox.ValueKind == JsonValueKind.Object)
        {
            return new AnnotationBounds(
                ReadFirstDouble(bbox, null, "x", "left", "x1"),
                ReadFirstDouble(bbox, null, "y", "top", "y1"),
                ReadWidth(bbox, null),
                ReadHeight(bbox, null));
        }

        if (element.TryGetProperty("rect", out var rect) && rect.ValueKind == JsonValueKind.Object)
        {
            return new AnnotationBounds(
                ReadFirstDouble(rect, null, "x", "left", "x1"),
                ReadFirstDouble(rect, null, "y", "top", "y1"),
                ReadWidth(rect, null),
                ReadHeight(rect, null));
        }

        return null;
    }

    private static double ReadWidth(JsonElement element, AnnotationBounds? bounds)
    {
        if (TryReadDouble(element, "width", out var width) || TryReadDouble(element, "w", out width))
        {
            return width;
        }

        if (TryReadDouble(element, "right", out var right))
        {
            var left = ReadFirstDouble(element, bounds, "x", "left", "x1");
            return Math.Max(0, right - left);
        }

        if (TryReadDouble(element, "x2", out var x2))
        {
            var x = ReadFirstDouble(element, bounds, "x", "left", "x1");
            return Math.Max(0, x2 - x);
        }

        return bounds?.Width ?? 0;
    }

    private static double ReadHeight(JsonElement element, AnnotationBounds? bounds)
    {
        if (TryReadDouble(element, "height", out var height) || TryReadDouble(element, "h", out height))
        {
            return height;
        }

        if (TryReadDouble(element, "bottom", out var bottom))
        {
            var top = ReadFirstDouble(element, bounds, "y", "top", "y1");
            return Math.Max(0, bottom - top);
        }

        if (TryReadDouble(element, "y2", out var y2))
        {
            var y = ReadFirstDouble(element, bounds, "y", "top", "y1");
            return Math.Max(0, y2 - y);
        }

        return bounds?.Height ?? 0;
    }

    private static string NormalizeType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "box" or "rectangle" or "rect" or "region" => "highlight_box",
            "callout" or "text" => "label",
            "note" => "note_panel",
            "solution" or "worked_solution" => "solution_panel",
            "explanation" => "explanation_panel",
            "force" or "vector" or "force_arrow" or "physics_arrow" => "force_vector",
            "fbd" or "free_body" or "body_diagram" or "freebody" => "free_body_diagram",
            _ => type.Trim()
        };
    }

    private static IReadOnlyList<ForceEntry>? ReadForces(JsonElement element)
    {
        if (!element.TryGetProperty("forces", out var forcesEl) || forcesEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<ForceEntry>();
        foreach (var f in forcesEl.EnumerateArray())
        {
            var label = ReadFirstFlexibleString(f, "label", "name", "text") ?? "F";
            TryReadDouble(f, "angle", out var angle);
            var magnitude = ReadFirstFlexibleString(f, "magnitude", "value", "mag");
            var color = ReadFirstFlexibleString(f, "color", "strokeColor");
            list.Add(new ForceEntry(label, angle, magnitude, color));
        }

        return list.Count > 0 ? list : null;
    }

    private sealed record AnnotationBounds(double X, double Y, double Width, double Height);
}
