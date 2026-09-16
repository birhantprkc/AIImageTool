using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ZeroVision.Host.Workspace;

public partial class DevelopPanel
{
    private Image? _imgLoupe;
    private TextBlock? _txtLoupeCoord;
    private float _loupeNormX = 0.5f;
    private float _loupeNormY = 0.5f;
    private bool _isDraggingLoupe;
    private Point _loupeDragStart;
    private BitmapSource? _latestRenderedSource;

    /// <summary>Fired when user clicks 'Pick Spot' to inspect a specific area from main photo canvas.</summary>
    public event EventHandler? DetailLoupePickRequested;

    /// <summary>Constructs the 100% Detail Loupe Inspector UI box to be placed inside the Detail group.</summary>
    private FrameworkElement BuildDetailLoupeWidget()
    {
        var container = new StackPanel { Margin = new Thickness(0, 2, 0, 8) };

        // Header row
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var title = new TextBlock
        {
            Text = "🔍 100% DETAIL LOUPE",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        header.Children.Add(title);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(rightStack, Dock.Right);

        _txtLoupeCoord = new TextBlock
        {
            Text = "50%, 50%",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _txtLoupeCoord.SetResourceReference(TextBlock.ForegroundProperty, "TextDimBrush");
        rightStack.Children.Add(_txtLoupeCoord);

        var btnPick = new Button
        {
            Content = "📍 Pick",
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            ToolTip = "Click on main photo to inspect fine detail at 1:1 scale"
        };
        btnPick.Click += (_, _) => DetailLoupePickRequested?.Invoke(this, EventArgs.Empty);
        rightStack.Children.Add(btnPick);

        var btnCenter = new Button
        {
            Content = "⌖ Center",
            Padding = new Thickness(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            ToolTip = "Reset loupe inspection point to center"
        };
        btnCenter.Click += (_, _) =>
        {
            _loupeNormX = 0.5f;
            _loupeNormY = 0.5f;
            UpdateDetailLoupe();
        };
        rightStack.Children.Add(btnCenter);

        header.Children.Add(rightStack);
        container.Children.Add(header);

        // Loupe Viewport Box
        var boxBorder = new Border
        {
            Height = 130,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
            Cursor = Cursors.SizeAll,
            ToolTip = "Drag inside to pan the 1:1 inspection view"
        };
        boxBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush_");

        var viewGrid = new Grid();

        _imgLoupe = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        viewGrid.Children.Add(_imgLoupe);

        // Center crosshair overlay (subtle indicators)
        var crossV = new Line
        {
            X1 = 0, Y1 = 0, X2 = 0, Y2 = 14,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        var crossH = new Line
        {
            X1 = 0, Y1 = 0, X2 = 14, Y2 = 0,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        viewGrid.Children.Add(crossV);
        viewGrid.Children.Add(crossH);

        // Interaction on Loupe Viewport: drag to pan
        boxBorder.MouseLeftButtonDown += (s, e) =>
        {
            _isDraggingLoupe = true;
            _loupeDragStart = e.GetPosition(boxBorder);
            boxBorder.CaptureMouse();
        };

        boxBorder.MouseMove += (s, e) =>
        {
            if (!_isDraggingLoupe || _latestRenderedSource == null) return;
            var cur = e.GetPosition(boxBorder);
            double dx = cur.X - _loupeDragStart.X;
            double dy = cur.Y - _loupeDragStart.Y;
            _loupeDragStart = cur;

            int w = Math.Max(1, _latestRenderedSource.PixelWidth);
            int h = Math.Max(1, _latestRenderedSource.PixelHeight);

            // Inverted drag to follow canvas content
            _loupeNormX = Math.Clamp(_loupeNormX - (float)(dx / w), 0.05f, 0.95f);
            _loupeNormY = Math.Clamp(_loupeNormY - (float)(dy / h), 0.05f, 0.95f);
            UpdateDetailLoupe();
        };

        boxBorder.MouseLeftButtonUp += (s, e) =>
        {
            _isDraggingLoupe = false;
            boxBorder.ReleaseMouseCapture();
        };

        boxBorder.Child = viewGrid;
        container.Children.Add(boxBorder);

        return container;
    }

    /// <summary>Applies a sampled spot point from main canvas click.</summary>
    public void ApplyDetailLoupePick(float nx, float ny)
    {
        _loupeNormX = Math.Clamp(nx, 0.05f, 0.95f);
        _loupeNormY = Math.Clamp(ny, 0.05f, 0.95f);
        UpdateDetailLoupe();
    }

    /// <summary>Updates the 1:1 Loupe crop with current develop rendered bitmap.</summary>
    public void UpdateDetailLoupe(BitmapSource? newSource = null)
    {
        if (newSource != null)
        {
            _latestRenderedSource = newSource;
        }

        if (_imgLoupe == null || _latestRenderedSource == null) return;

        try
        {
            int origW = _latestRenderedSource.PixelWidth;
            int origH = _latestRenderedSource.PixelHeight;
            if (origW <= 0 || origH <= 0) return;

            const int patchW = 260;
            const int patchH = 130;

            int curW = Math.Min(patchW, origW);
            int curH = Math.Min(patchH, origH);

            int cx = (int)(_loupeNormX * origW);
            int cy = (int)(_loupeNormY * origH);

            int x = Math.Clamp(cx - curW / 2, 0, Math.Max(0, origW - curW));
            int y = Math.Clamp(cy - curH / 2, 0, Math.Max(0, origH - curH));

            var cropped = new CroppedBitmap(_latestRenderedSource, new Int32Rect(x, y, curW, curH));
            _imgLoupe.Source = cropped;

            if (_txtLoupeCoord != null)
            {
                _txtLoupeCoord.Text = $"{_loupeNormX * 100:0}%, {_loupeNormY * 100:0}%";
            }
        }
        catch
        {
            // Graceful fallback if crop rect exceeds bounds during fast resize
        }
    }
}
