using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GlobalCaptureAssistant.Capture;

public partial class CaptureOverlayWindow : Window
{
    private System.Windows.Point? _dragStart;
    private Rect _selectionRect;

    public CaptureOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += OnKeyDown;
    }

    public Rect SelectionRect => _selectionRect;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Focus();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _selectionRect = new Rect(_dragStart.Value, current);
        SelectionRectVisual.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectVisual, _selectionRect.X);
        Canvas.SetTop(SelectionRectVisual, _selectionRect.Y);
        SelectionRectVisual.Width = Math.Abs(_selectionRect.Width);
        SelectionRectVisual.Height = Math.Abs(_selectionRect.Height);

        HintText.Text = $"{(int)Math.Abs(_selectionRect.Width)} x {(int)Math.Abs(_selectionRect.Height)}";
        Canvas.SetLeft(HintCard, Math.Min(_selectionRect.Right + 12, Width - 180));
        Canvas.SetTop(HintCard, Math.Max(16, _selectionRect.Top - 36));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        ReleaseMouseCapture();
        _dragStart = null;
        if (Math.Abs(_selectionRect.Width) < 3 || Math.Abs(_selectionRect.Height) < 3)
        {
            _selectionRect = Rect.Empty;
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
            _selectionRect = Rect.Empty;
            DialogResult = false;
            Close();
            return;
        }

        if (e.Key == Key.Enter && !_selectionRect.IsEmpty)
        {
            DialogResult = true;
            Close();
        }
    }
}
