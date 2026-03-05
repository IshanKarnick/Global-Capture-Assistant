using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;

namespace GlobalCaptureAssistant.Capture;

public sealed class OverlayCaptureService
{
    public Task<CaptureResult> CaptureAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<CaptureResult>(cancellationToken);
        }

        var overlay = new CaptureOverlayWindow();
        var result = overlay.ShowDialog();
        if (result != true || overlay.SelectionRect.IsEmpty)
        {
            return Task.FromResult(new CaptureResult(true, null));
        }

        var png = CaptureRegion(overlay.SelectionRect);
        return Task.FromResult(new CaptureResult(false, png));
    }

    private static byte[] CaptureRegion(Rect rect)
    {
        var x = (int)Math.Round(rect.X + SystemParameters.VirtualScreenLeft);
        var y = (int)Math.Round(rect.Y + SystemParameters.VirtualScreenTop);
        var width = Math.Max(1, (int)Math.Round(Math.Abs(rect.Width)));
        var height = Math.Max(1, (int)Math.Round(Math.Abs(rect.Height)));

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
