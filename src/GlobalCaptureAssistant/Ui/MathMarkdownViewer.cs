using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using WpfMath.Controls;

// Explicit aliases to resolve WPF vs WinForms ambiguity (UseWindowsForms=true in csproj)
using UserControl          = System.Windows.Controls.UserControl;
using StackPanel           = System.Windows.Controls.StackPanel;
using TextBlock            = System.Windows.Controls.TextBlock;
using Border               = System.Windows.Controls.Border;
using ScrollBarVisibility  = System.Windows.Controls.ScrollBarVisibility;
using MarkdownScrollViewer = MdXaml.MarkdownScrollViewer;

namespace GlobalCaptureAssistant.Ui;

/// <summary>
/// Renders markdown with WpfMath for all LaTeX math ($$, $, \[, \().
/// Block equations get their own centered row; inline equations are rendered
/// within the flow but may appear on a separate line given WPF layout constraints.
/// </summary>
public sealed class MathMarkdownViewer : UserControl
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

    // ── Regex — block delimiters FIRST so $$ isn't matched as two $'s ─────────

    private static readonly Regex MathSplitter = new(
        @"(\$\$[\s\S]+?\$\$|\\\[[\s\S]+?\\\]|\$[^\$\n]+?\$|\\\([^\)]+?\\\))",
        RegexOptions.Compiled);

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

        int last = 0;
        foreach (Match m in MathSplitter.Matches(text))
        {
            if (m.Index > last)
            {
                var mdText = text[last..m.Index];
                if (!string.IsNullOrWhiteSpace(mdText))
                    _panel.Children.Add(BuildMarkdownElement(mdText));
            }

            string raw = m.Value;
            bool isBlock = raw.StartsWith("$$", StringComparison.Ordinal)
                        || raw.StartsWith(@"\[", StringComparison.Ordinal);
            string inner = StripDelimiters(raw);
            _panel.Children.Add(BuildMathElement(inner, isBlock));
            last = m.Index + m.Length;
        }

        if (last < text.Length)
        {
            var tail = text[last..];
            if (!string.IsNullOrWhiteSpace(tail))
                _panel.Children.Add(BuildMarkdownElement(tail));
        }
    }

    private static string StripDelimiters(string raw)
    {
        if (raw.StartsWith("$$",  StringComparison.Ordinal) && raw.EndsWith("$$",  StringComparison.Ordinal)) return raw[2..^2].Trim();
        if (raw.StartsWith(@"\[", StringComparison.Ordinal) && raw.EndsWith(@"\]", StringComparison.Ordinal)) return raw[2..^2].Trim();
        if (raw.StartsWith(@"\(", StringComparison.Ordinal) && raw.EndsWith(@"\)", StringComparison.Ordinal)) return raw[2..^2].Trim();
        if (raw.StartsWith("$",   StringComparison.Ordinal) && raw.EndsWith("$",   StringComparison.Ordinal)) return raw[1..^1].Trim();
        return raw;
    }

    private UIElement BuildMarkdownElement(string mdText)
    {
        var viewer = new MarkdownScrollViewer
        {
            Markdown                      = mdText.Trim(),
            VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background                    = System.Windows.Media.Brushes.Transparent,
            Padding                       = new Thickness(0),
            Margin                        = new Thickness(0),
            FontSize                      = FontSize,
            Foreground                    = Foreground,
        };
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
                Scale             = isBlock ? 16 : 13,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };
            if (isBlock)
            {
                return new Border
                {
                    Child               = ctrl,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Padding             = new Thickness(8, 6, 8, 6),
                    Margin              = new Thickness(0, 4, 0, 4),
                };
            }
            ctrl.Margin = new Thickness(0, 1, 0, 1);
            return ctrl;
        }
        catch
        {
            return new TextBlock
            {
                Text         = $"${formula}$",
                FontFamily   = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize     = FontSize,
                Foreground   = Foreground,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 1, 0, 1),
            };
        }
    }

    // ── Mouse-wheel passthrough ───────────────────────────────────────────────

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
        ((UIElement?)((FrameworkElement)this).Parent)?.RaiseEvent(
            new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source      = this,
            });
    }
}
