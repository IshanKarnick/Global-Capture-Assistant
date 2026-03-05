using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MdXaml;
using WpfMath.Controls;

namespace GlobalCaptureAssistant.Ui;

/// <summary>
/// A markdown viewer that additionally renders LaTeX math expressions
/// ($…$, $$…$$, \(…\), \[…\]) using WpfMath.
/// Non-math segments are rendered by MdXaml.
/// </summary>
public sealed class MathMarkdownViewer : System.Windows.Controls.UserControl
{
    // ── Dependency property ───────────────────────────────────────────────────

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MathMarkdownViewer),
            new FrameworkPropertyMetadata(string.Empty, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    // ── Math split pattern ────────────────────────────────────────────────────

    // Order matters: block delimiters must come before inline ones.
    private static readonly Regex MathSplitter = new(
        @"(\$\$.+?\$\$|\\\[.+?\\\]|\$.+?\$|\\\(.+?\\\))",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly StackPanel _panel = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public MathMarkdownViewer()
    {
        Content = _panel;
        Focusable = false;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MathMarkdownViewer)d).Render((string)(e.NewValue ?? string.Empty));

    private void Render(string text)
    {
        _panel.Children.Clear();
        if (string.IsNullOrEmpty(text)) return;

        foreach (var seg in Split(text))
        {
            if (seg.IsMath)
                _panel.Children.Add(BuildMathElement(seg.Content, seg.IsBlock));
            else if (!string.IsNullOrWhiteSpace(seg.Content))
                _panel.Children.Add(BuildMarkdownElement(seg.Content));
        }
    }

    private UIElement BuildMarkdownElement(string mdText)
    {
        var viewer = new MarkdownScrollViewer
        {
            Markdown = mdText.Trim(),
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = System.Windows.Media.Brushes.Transparent,
            Padding    = new Thickness(0),
            Margin     = new Thickness(0),
            FontSize   = FontSize,
            Foreground = Foreground,
        };

        // Bubble mouse-wheel events to our parent ScrollViewer
        viewer.PreviewMouseWheel += PassMouseWheel;
        return viewer;
    }

    private UIElement BuildMathElement(string formula, bool isBlock)
    {
        try
        {
            var ctrl = new FormulaControl
            {
                Formula           = formula,
                Scale             = 14,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin            = isBlock
                    ? new Thickness(0, 6, 0, 6)
                    : new Thickness(2, 2, 2, 2),
            };

            return isBlock
                ? (UIElement)new Border
                  {
                      Child               = ctrl,
                      HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                      Padding             = new Thickness(8, 4, 8, 4),
                  }
                : ctrl;
        }
        catch
        {
            // WpfMath can't parse the formula — fall back to monospace text
            return new TextBlock
            {
                Text        = $"${formula}$",
                FontFamily  = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize    = FontSize,
                Foreground  = Foreground,
                TextWrapping = TextWrapping.Wrap,
                Margin      = new Thickness(0, 2, 0, 2),
            };
        }
    }

    // ── Math/text splitting ───────────────────────────────────────────────────

    private static IEnumerable<Segment> Split(string text)
    {
        int last = 0;
        foreach (Match m in MathSplitter.Matches(text))
        {
            if (m.Index > last)
                yield return new Segment(text[last..m.Index], false, false);

            string raw = m.Value;
            bool isBlock = raw.StartsWith("$$", StringComparison.Ordinal)
                        || raw.StartsWith(@"\[", StringComparison.Ordinal);
            string inner = StripDelimiters(raw);
            yield return new Segment(inner, true, isBlock);

            last = m.Index + m.Length;
        }

        if (last < text.Length)
            yield return new Segment(text[last..], false, false);
    }

    private static string StripDelimiters(string raw)
    {
        if (raw.StartsWith("$$", StringComparison.Ordinal) && raw.EndsWith("$$", StringComparison.Ordinal))
            return raw[2..^2].Trim();
        if (raw.StartsWith(@"\[", StringComparison.Ordinal) && raw.EndsWith(@"\]", StringComparison.Ordinal))
            return raw[2..^2].Trim();
        if (raw.StartsWith(@"\(", StringComparison.Ordinal) && raw.EndsWith(@"\)", StringComparison.Ordinal))
            return raw[2..^2].Trim();
        if (raw.StartsWith("$", StringComparison.Ordinal) && raw.EndsWith("$", StringComparison.Ordinal))
            return raw[1..^1].Trim();
        return raw;
    }

    // ── Mouse wheel passthrough ───────────────────────────────────────────────

    private void PassMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source      = this,
        });
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled) return;
        e.Handled = true;
        var parent = (UIElement)((FrameworkElement)this).Parent;
        parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source      = this,
        });
    }

    // ── Segment record ────────────────────────────────────────────────────────

    private readonly record struct Segment(string Content, bool IsMath, bool IsBlock);
}
