using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OneNoteAnalyzeAddIn.Capture;

public partial class OverlayCaptureWindow : Window
{
    private System.Windows.Point? _dragStartPoint;
    private Rect _selection;

    public OverlayCaptureWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    public Rect Selection => _selection;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Focus();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStartPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _selection = new Rect(_dragStartPoint.Value, current);
        DrawSelection(_selection);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStartPoint is null)
        {
            return;
        }

        ReleaseMouseCapture();
        _dragStartPoint = null;
        if (_selection.Width < 4 || _selection.Height < 4)
        {
            _selection = Rect.Empty;
            SelectionRectVisual.Visibility = Visibility.Collapsed;
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _selection = Rect.Empty;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter && !_selection.IsEmpty)
        {
            DialogResult = true;
            Close();
        }
    }

    private void DrawSelection(Rect rect)
    {
        SelectionRectVisual.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectVisual, rect.X);
        Canvas.SetTop(SelectionRectVisual, rect.Y);
        SelectionRectVisual.Width = Math.Abs(rect.Width);
        SelectionRectVisual.Height = Math.Abs(rect.Height);

        if (HintCard is not null)
        {
            HintText.Text = $"{(int)Math.Abs(rect.Width)} x {(int)Math.Abs(rect.Height)}";
            var hintX = Math.Min(rect.Right + 12, Width - 180);
            var hintY = Math.Max(16, rect.Top - 40);
            Canvas.SetLeft(HintCard, hintX);
            Canvas.SetTop(HintCard, hintY);
        }
    }
}
