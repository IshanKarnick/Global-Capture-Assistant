using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace GlobalCaptureAssistant.Ui;

public sealed class NoteCardRenderWindow : Window
{
    private readonly TaskCompletionSource _loadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WebView2 _webView;

    public NoteCardRenderWindow()
    {
        Width = 900;
        Height = 1200;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Left = -20000;
        Top = -20000;
        Background = System.Windows.Media.Brushes.White;

        _webView = new WebView2
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch
        };

        Content = _webView;
        Loaded += (_, _) => _loadedTcs.TrySetResult();
    }

    public async Task<byte[]> RenderHtmlToPngAsync(string html, CancellationToken cancellationToken)
    {
        EnsureShownOffscreen();
        await _loadedTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);

        ConfigureWebView();
        await NavigateAsync(html, cancellationToken).ConfigureAwait(true);
        await WaitForStableRenderAsync(cancellationToken).ConfigureAwait(true);

        var size = await MeasureCardAsync(cancellationToken).ConfigureAwait(true);
        ResizeForCapture(size.width, size.height);
        await WaitForStableRenderAsync(cancellationToken).ConfigureAwait(true);

        using var stream = new MemoryStream();
        await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream).ConfigureAwait(true);
        return stream.ToArray();
    }

    private void EnsureShownOffscreen()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    private void ConfigureWebView()
    {
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
    }

    private async Task NavigateAsync(string html, CancellationToken cancellationToken)
    {
        var navigationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                navigationTcs.TrySetResult();
            }
            else
            {
                navigationTcs.TrySetException(new InvalidOperationException($"WebView2 navigation failed with status {args.WebErrorStatus}."));
            }
        }

        _webView.NavigationCompleted += OnNavigationCompleted;
        try
        {
            _webView.NavigateToString(html);
            await navigationTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _webView.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private async Task WaitForStableRenderAsync(CancellationToken cancellationToken)
    {
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        const string renderReadyScript =
            """
            (async () => {
              if (document.fonts && document.fonts.ready) {
                await document.fonts.ready;
              }
              await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
              return true;
            })();
            """;

        _ = await _webView.ExecuteScriptAsync(renderReadyScript).WaitAsync(cancellationToken).ConfigureAwait(true);
        await Task.Delay(120, cancellationToken).ConfigureAwait(true);
    }

    private async Task<(int width, int height)> MeasureCardAsync(CancellationToken cancellationToken)
    {
        const string measureScript =
            """
            (() => {
              const root = document.getElementById('note-card-root');
              if (!root) {
                return { width: 840, height: 1100 };
              }

              const rect = root.getBoundingClientRect();
              return {
                width: Math.ceil(rect.width),
                height: Math.ceil(rect.height)
              };
            })();
            """;

        var rawResult = await _webView.ExecuteScriptAsync(measureScript).WaitAsync(cancellationToken).ConfigureAwait(true);
        using var doc = JsonDocument.Parse(rawResult);
        var width = doc.RootElement.TryGetProperty("width", out var widthValue) ? widthValue.GetInt32() : 840;
        var height = doc.RootElement.TryGetProperty("height", out var heightValue) ? heightValue.GetInt32() : 1100;
        return (Math.Clamp(width + 24, 320, 1200), Math.Clamp(height + 24, 320, 1600));
    }

    private void ResizeForCapture(int width, int height)
    {
        Width = width;
        Height = height;
        _webView.Width = width;
        _webView.Height = height;
        UpdateLayout();
    }
}
