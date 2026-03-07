using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GlobalCaptureAssistant.Models;
using WpfMath.Controls;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace GlobalCaptureAssistant.Ui;

public sealed class AnnotatedScreenWindow : Window
{
    private const double RailWidth = 340;
    private const double RailPadding = 18;
    private const double RailGap = 28;
    private const double CalloutGap = 14;
    private const double WindowPadding = 20;
    private const double CalloutMaxBodyHeight = 240;

    public AnnotatedScreenWindow(BitmapSource screenshot, ScreenAnnotationDocument document)
    {
        Title = "Screen Annotation";
        Width = 1500;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 600;
        Background = new SolidColorBrush(WpfColor.FromRgb(12, 14, 18));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var imageWidth = screenshot.PixelWidth;
        var imageHeight = screenshot.PixelHeight;
        var imageOffsetX = RailWidth + RailGap;
        var totalWidth = imageWidth + (RailWidth * 2) + (RailGap * 2);

        var rootCanvas = new Canvas
        {
            Width = totalWidth,
            Height = imageHeight
        };

        rootCanvas.Children.Add(new Border
        {
            Width = totalWidth,
            Height = imageHeight,
            Background = new SolidColorBrush(WpfColor.FromRgb(12, 14, 18))
        });

        rootCanvas.Children.Add(new Border
        {
            Width = imageWidth + 12,
            Height = imageHeight + 12,
            Background = new SolidColorBrush(WpfColor.FromRgb(20, 24, 30)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Effect = BuildShadow(0.32),
            Child = new Border
            {
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Child = new WpfImage
                {
                    Source = screenshot,
                    Width = imageWidth,
                    Height = imageHeight,
                    Stretch = Stretch.Fill
                }
            }
        });
        Canvas.SetLeft(rootCanvas.Children[^1], imageOffsetX - 6);
        Canvas.SetTop(rootCanvas.Children[^1], -6);

        rootCanvas.Children.Add(new WpfRectangle
        {
            Width = imageWidth,
            Height = imageHeight,
            Fill = new SolidColorBrush(WpfColor.FromArgb(54, 6, 10, 16))
        });
        Canvas.SetLeft(rootCanvas.Children[^1], imageOffsetX);
        Canvas.SetTop(rootCanvas.Children[^1], 0);

        rootCanvas.Children.Add(new Border
        {
            Width = RailWidth,
            Height = imageHeight,
            Background = new SolidColorBrush(WpfColor.FromArgb(70, 20, 24, 30)),
            CornerRadius = new CornerRadius(18)
        });
        Canvas.SetLeft(rootCanvas.Children[^1], 0);
        Canvas.SetTop(rootCanvas.Children[^1], 0);

        rootCanvas.Children.Add(new Border
        {
            Width = RailWidth,
            Height = imageHeight,
            Background = new SolidColorBrush(WpfColor.FromArgb(70, 20, 24, 30)),
            CornerRadius = new CornerRadius(18)
        });
        Canvas.SetLeft(rootCanvas.Children[^1], totalWidth - RailWidth);
        Canvas.SetTop(rootCanvas.Children[^1], 0);

        RenderAnnotations(rootCanvas, document.Annotations, imageOffsetX, imageWidth, imageHeight, totalWidth);

        Content = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromRgb(12, 14, 18)),
            Padding = new Thickness(WindowPadding),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = rootCanvas
            }
        };
    }

    private static void RenderAnnotations(
        Canvas canvas,
        IReadOnlyList<ScreenAnnotationItem> annotations,
        double imageOffsetX,
        double imageWidth,
        double imageHeight,
        double totalWidth)
    {
        var leftCursorY = RailPadding;
        var rightCursorY = RailPadding;

        foreach (var annotation in annotations)
        {
            if (IsHighlight(annotation))
            {
                RenderHighlight(canvas, annotation, imageOffsetX, imageWidth, imageHeight);
            }
        }

        foreach (var annotation in annotations)
        {
            RenderSideCallout(
                canvas,
                annotation,
                imageOffsetX,
                imageWidth,
                imageHeight,
                totalWidth,
                ref leftCursorY,
                ref rightCursorY);
        }
    }

    private static bool IsHighlight(ScreenAnnotationItem annotation)
    {
        return annotation.Type.Trim().Equals("highlight_box", StringComparison.OrdinalIgnoreCase);
    }

    private static void RenderHighlight(Canvas canvas, ScreenAnnotationItem annotation, double imageOffsetX, double imageWidth, double imageHeight)
    {
        var stroke = ResolveBrush(annotation.Color, annotation.Emphasis);
        var fill = stroke.Clone();
        fill.Opacity = 0.10;

        var x = imageOffsetX + (Clamp01(annotation.X) * imageWidth);
        var y = Clamp01(annotation.Y) * imageHeight;
        var width = Math.Max(44, Clamp01(annotation.Width) * imageWidth);
        var height = Math.Max(30, Clamp01(annotation.Height) * imageHeight);

        var halo = new WpfRectangle
        {
            Width = width,
            Height = height,
            Stroke = new SolidColorBrush(WpfColor.FromArgb(170, 8, 10, 16)),
            StrokeThickness = 7,
            RadiusX = 12,
            RadiusY = 12
        };

        var rectangle = new WpfRectangle
        {
            Width = width,
            Height = height,
            Stroke = stroke,
            StrokeThickness = 3,
            Fill = fill,
            RadiusX = 10,
            RadiusY = 10
        };

        Canvas.SetLeft(halo, x);
        Canvas.SetTop(halo, y);
        canvas.Children.Add(halo);

        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        canvas.Children.Add(rectangle);
    }

    private static void RenderSideCallout(
        Canvas canvas,
        ScreenAnnotationItem annotation,
        double imageOffsetX,
        double imageWidth,
        double imageHeight,
        double totalWidth,
        ref double leftCursorY,
        ref double rightCursorY)
    {
        var accent = ResolveBrush(annotation.Color, annotation.Emphasis);
        var target = ResolveTarget(annotation, imageOffsetX, imageWidth, imageHeight);
        var body = BuildCalloutBody(annotation);
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var placeLeft = target.X < imageOffsetX + (imageWidth / 2);
        var x = placeLeft ? RailPadding : totalWidth - RailWidth + RailPadding;
        var width = RailWidth - (RailPadding * 2);

        var callout = BuildCallout(annotation, accent, body, width);
        callout.Measure(new System.Windows.Size(width, imageHeight - (RailPadding * 2)));

        var desiredHeight = Math.Max(84, Math.Min(callout.DesiredSize.Height, imageHeight - (RailPadding * 2)));
        var y = placeLeft ? leftCursorY : rightCursorY;
        if (y + desiredHeight > imageHeight - RailPadding)
        {
            y = Math.Max(RailPadding, imageHeight - desiredHeight - RailPadding);
        }

        callout.Height = desiredHeight;
        Canvas.SetLeft(callout, x);
        Canvas.SetTop(callout, y);
        canvas.Children.Add(callout);

        if (placeLeft)
        {
            leftCursorY = Math.Min(imageHeight - RailPadding, y + desiredHeight + CalloutGap);
        }
        else
        {
            rightCursorY = Math.Min(imageHeight - RailPadding, y + desiredHeight + CalloutGap);
        }

        var arrowStart = placeLeft
            ? new WpfPoint(x + width, y + Math.Min(52, desiredHeight / 2))
            : new WpfPoint(x, y + Math.Min(52, desiredHeight / 2));

        RenderConnector(canvas, arrowStart, target, accent);
        RenderTargetDot(canvas, target, accent);
    }

    private static Border BuildCallout(ScreenAnnotationItem annotation, WpfBrush accent, string body, double width)
    {
        var textBrush = new SolidColorBrush(WpfColor.FromRgb(22, 28, 36));
        var chrome = new Grid();
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accentBar = new Border
        {
            Background = accent,
            CornerRadius = new CornerRadius(12, 0, 0, 12)
        };
        Grid.SetColumn(accentBar, 0);
        chrome.Children.Add(accentBar);

        var content = new StackPanel();
        var title = string.IsNullOrWhiteSpace(annotation.Title) ? GetDefaultTitle(annotation.Type) : annotation.Title!.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            content.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = textBrush,
                FontFamily = new WpfFontFamily("Segoe UI Variable Display"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        content.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = CalloutMaxBodyHeight,
            Content = new MathMarkdownViewer
            {
                Markdown = body,
                FontSize = 13.5,
                Foreground = textBrush
            }
        });

        var panelBody = new Border
        {
            Background = ResolveCalloutBackground(annotation.Type),
            BorderBrush = accent,
            BorderThickness = new Thickness(0, 2, 2, 2),
            CornerRadius = new CornerRadius(0, 16, 16, 0),
            Padding = new Thickness(14, 12, 14, 12),
            Child = content
        };
        Grid.SetColumn(panelBody, 1);
        chrome.Children.Add(panelBody);

        return new Border
        {
            Width = width,
            CornerRadius = new CornerRadius(16),
            Effect = BuildShadow(0.35),
            Child = chrome
        };
    }

    private static string BuildCalloutBody(ScreenAnnotationItem annotation)
    {
        var text = annotation.Text?.Trim();
        var latex = annotation.Latex?.Trim();
        var title = annotation.Title?.Trim();

        if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(latex))
        {
            return $"{text}\n\n$${latex}$$";
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (!string.IsNullOrWhiteSpace(latex))
        {
            return $"$${latex}$$";
        }

        return title ?? string.Empty;
    }

    private static void RenderConnector(Canvas canvas, WpfPoint start, WpfPoint end, WpfBrush accent)
    {
        var curve = BuildConnectorGeometry(start, end);

        var underlay = new Path
        {
            Data = curve,
            Stroke = new SolidColorBrush(WpfColor.FromArgb(190, 8, 10, 16)),
            StrokeThickness = 9,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        canvas.Children.Add(underlay);

        var line = new Path
        {
            Data = curve,
            Stroke = accent,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        canvas.Children.Add(line);

        canvas.Children.Add(BuildConnectorOrigin(start, accent));
        canvas.Children.Add(BuildArrowHead(start, end, accent));
    }

    private static Geometry BuildConnectorGeometry(WpfPoint start, WpfPoint end)
    {
        var horizontalDirection = end.X >= start.X ? 1d : -1d;
        var distanceX = Math.Abs(end.X - start.X);
        var controlOffset = Math.Max(42, Math.Min(120, distanceX * 0.45));

        var control1 = new WpfPoint(start.X + (horizontalDirection * controlOffset), start.Y);
        var control2 = new WpfPoint(end.X - (horizontalDirection * controlOffset), end.Y);

        var figure = new PathFigure
        {
            StartPoint = start,
            Segments =
            [
                new BezierSegment(control1, control2, end, true)
            ]
        };

        return new PathGeometry([figure]);
    }

    private static UIElement BuildConnectorOrigin(WpfPoint start, WpfBrush accent)
    {
        var glow = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new SolidColorBrush(WpfColor.FromArgb(90, 255, 255, 255)),
            Stroke = accent,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(glow, start.X - 7);
        Canvas.SetTop(glow, start.Y - 7);
        return glow;
    }

    private static Polygon BuildArrowHead(WpfPoint start, WpfPoint end, WpfBrush fill)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        const double size = 14;

        return new Polygon
        {
            Fill = fill,
            Points =
            [
                new WpfPoint(end.X, end.Y),
                new WpfPoint(end.X - size * Math.Cos(angle - Math.PI / 8), end.Y - size * Math.Sin(angle - Math.PI / 8)),
                new WpfPoint(end.X - (size * 0.35) * Math.Cos(angle), end.Y - (size * 0.35) * Math.Sin(angle)),
                new WpfPoint(end.X - size * Math.Cos(angle + Math.PI / 8), end.Y - size * Math.Sin(angle + Math.PI / 8))
            ]
        };
    }

    private static void RenderTargetDot(Canvas canvas, WpfPoint target, WpfBrush accent)
    {
        var ring = new Ellipse
        {
            Width = 22,
            Height = 22,
            Stroke = accent,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(WpfColor.FromArgb(22, 255, 255, 255))
        };
        Canvas.SetLeft(ring, target.X - 11);
        Canvas.SetTop(ring, target.Y - 11);
        canvas.Children.Add(ring);

        var halo = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new SolidColorBrush(WpfColor.FromArgb(190, 8, 10, 16))
        };
        Canvas.SetLeft(halo, target.X - 8);
        Canvas.SetTop(halo, target.Y - 8);
        canvas.Children.Add(halo);

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = accent
        };
        Canvas.SetLeft(dot, target.X - 5);
        Canvas.SetTop(dot, target.Y - 5);
        canvas.Children.Add(dot);
    }

    private static WpfPoint ResolveTarget(ScreenAnnotationItem annotation, double imageOffsetX, double imageWidth, double imageHeight)
    {
        var type = annotation.Type.Trim().ToLowerInvariant();
        var x = Clamp01(annotation.X) * imageWidth;
        var y = Clamp01(annotation.Y) * imageHeight;

        if (type == "arrow" && annotation.EndX.HasValue && annotation.EndY.HasValue)
        {
            x = Clamp01(annotation.EndX.Value) * imageWidth;
            y = Clamp01(annotation.EndY.Value) * imageHeight;
        }
        else if (type == "highlight_box")
        {
            x += (Clamp01(annotation.Width) * imageWidth) / 2;
            y += (Clamp01(annotation.Height) * imageHeight) / 2;
        }

        return new WpfPoint(imageOffsetX + x, y);
    }

    private static SolidColorBrush ResolveBrush(string? explicitColor, string? emphasis)
    {
        if (!string.IsNullOrWhiteSpace(explicitColor))
        {
            try
            {
                var color = (WpfColor)WpfColorConverter.ConvertFromString(explicitColor)!;
                return new SolidColorBrush(color);
            }
            catch
            {
            }
        }

        return (emphasis ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "warning" => new SolidColorBrush(WpfColor.FromRgb(255, 184, 77)),
            "success" => new SolidColorBrush(WpfColor.FromRgb(98, 214, 156)),
            "equation" => new SolidColorBrush(WpfColor.FromRgb(155, 192, 255)),
            _ => new SolidColorBrush(WpfColor.FromRgb(255, 107, 129))
        };
    }

    private static SolidColorBrush ResolveCalloutBackground(string annotationType)
    {
        return annotationType.Trim().ToLowerInvariant() switch
        {
            "solution_panel" => new SolidColorBrush(WpfColor.FromRgb(244, 250, 246)),
            "note_panel" => new SolidColorBrush(WpfColor.FromRgb(252, 247, 236)),
            "equation" => new SolidColorBrush(WpfColor.FromRgb(241, 246, 255)),
            _ => new SolidColorBrush(WpfColor.FromRgb(248, 249, 252))
        };
    }

    private static string GetDefaultTitle(string annotationType)
    {
        return annotationType.Trim().ToLowerInvariant() switch
        {
            "highlight_box" => "Highlight",
            "solution_panel" => "Worked Solution",
            "note_panel" => "Notes",
            "explanation_panel" => "Explanation",
            "equation" => "Equation",
            _ => "Callout"
        };
    }

    private static Effect BuildShadow(double opacity)
    {
        return new DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 8,
            Direction = 270,
            Color = WpfColor.FromArgb(255, 0, 0, 0),
            Opacity = opacity
        };
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0, Math.Min(value, 1));
    }
}
