using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace GlobalCaptureAssistant.Platform;

internal static class AppIconLoader
{
    private static readonly Uri TrayIconUri = new("pack://application:,,,/Assets/Icons/tray.png", UriKind.Absolute);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon? LoadTrayIcon()
    {
        try
        {
            var resourceInfo = System.Windows.Application.GetResourceStream(TrayIconUri);
            if (resourceInfo?.Stream is null)
            {
                return null;
            }

            using var stream = resourceInfo.Stream;
            using var source = new Bitmap(stream);
            using var traySized = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(traySized))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, 16, 16));
            }

            var hIcon = traySized.GetHicon();
            try
            {
                using var fromHandle = Icon.FromHandle(hIcon);
                return (Icon)fromHandle.Clone();
            }
            finally
            {
                _ = DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }
}
