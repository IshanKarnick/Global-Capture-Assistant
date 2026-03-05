using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.Capture;

public sealed class OverlayCaptureService : IOverlayCaptureService
{
    public Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<CaptureResult>(cancellationToken);
        }

        var window = new OverlayCaptureWindow();
        var result = window.ShowDialog();
        if (result != true || window.Selection.IsEmpty)
        {
            return Task.FromResult(CaptureResult.Canceled());
        }

        var captured = CaptureScreenRect(window.Selection);
        return Task.FromResult(captured);
    }

    private static CaptureResult CaptureScreenRect(Rect localRect)
    {
        var x = (int)Math.Round(localRect.X + SystemParameters.VirtualScreenLeft);
        var y = (int)Math.Round(localRect.Y + SystemParameters.VirtualScreenTop);
        var width = Math.Max(1, (int)Math.Round(Math.Abs(localRect.Width)));
        var height = Math.Max(1, (int)Math.Round(Math.Abs(localRect.Height)));

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new CaptureResult(false, stream.ToArray(), new RectInt(x, y, width, height));
    }
}
