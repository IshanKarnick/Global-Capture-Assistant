namespace OneNoteAnalyzeAddIn.Models;

public sealed record CaptureResult(bool IsCanceled, byte[]? PngBytes, RectInt? ScreenRect)
{
    public static CaptureResult Canceled() => new(true, null, null);
}

public sealed record RectInt(int X, int Y, int Width, int Height);
