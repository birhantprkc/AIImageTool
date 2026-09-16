using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZeroVision.Imaging;

namespace ZeroVision.Host.Workspace;

public partial class CenterPreview
{
    private LocalMask? _gizmoMask;

    private enum MaskHandleKind { None, Center, Start, End, Top, Bottom, Left, Right }
    private MaskHandleKind _dragHandle = MaskHandleKind.None;
    private Point _gizmoMouseStart;
    private Dictionary<string, string> _gizmoInitParams = new();

    public void BindMaskGizmo(DevelopPanel panel)
    {
        _developPanel = panel;
        panel.ActiveMaskChanged += (s, mask) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (mask != null && (mask.MaskType == LinearGradientMask.Type || mask.MaskType == RadialMask.Type))
                {
                    _gizmoMask = mask;
                    maskGizmoOverlay.Visibility = Visibility.Visible;
                    RedrawMaskGizmo();
                }
                else
                {
                    _gizmoMask = null;
                    maskGizmoOverlay.Visibility = Visibility.Collapsed;
                    maskGizmoOverlay.Children.Clear();
                }
            });
        };
    }

    private Point NormToCanvas(Point norm)
    {
        if (imgPreview.Source == null || imgPreview.ActualWidth <= 0 || imgPreview.ActualHeight <= 0)
            return new Point(0, 0);

        var imgPoint = new Point(norm.X * imgPreview.ActualWidth, norm.Y * imgPreview.ActualHeight);
        return imgPreview.TranslatePoint(imgPoint, maskGizmoOverlay);
    }

    private Point CanvasToNorm(Point canvasPoint)
    {
        if (imgPreview.Source == null || imgPreview.ActualWidth <= 0 || imgPreview.ActualHeight <= 0)
            return new Point(0, 0);

        var imgPoint = maskGizmoOverlay.TranslatePoint(canvasPoint, imgPreview);
        return new Point(
            Math.Clamp(imgPoint.X / imgPreview.ActualWidth, 0.0, 1.0),
            Math.Clamp(imgPoint.Y / imgPreview.ActualHeight, 0.0, 1.0)
        );
    }

    private void RedrawMaskGizmo()
    {
        maskGizmoOverlay.Children.Clear();
        if (_gizmoMask == null || imgPreview.Source == null || imgPreview.ActualWidth <= 0 || imgPreview.ActualHeight <= 0)
            return;

        if (_gizmoMask.MaskType == LinearGradientMask.Type)
        {
            DrawLinearGradientGizmo();
        }
        else if (_gizmoMask.MaskType == RadialMask.Type)
        {
            DrawRadialGizmo();
        }
    }

    private void DrawLinearGradientGizmo()
    {
        if (_gizmoMask == null) return;
        var p = _gizmoMask.MaskParams;
        double x0 = ParseDouble(p, "x0", 0.5);
        double y0 = ParseDouble(p, "y0", 0.1);
        double x1 = ParseDouble(p, "x1", 0.5);
        double y1 = ParseDouble(p, "y1", 0.6);

        Point p0 = NormToCanvas(new Point(x0, y0));
        Point p1 = NormToCanvas(new Point(x1, y1));
        Point mid = new Point((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);

        Vector dir = p1 - p0;
        double len = dir.Length;
        Vector normal = len > 1e-4 ? new Vector(-dir.Y / len, dir.X / len) : new Vector(1, 0);
        double barLen = Math.Max(260.0, len * 1.5);

        // Bar Start (p0)
        AddGizmoLine(p0 - normal * barLen, p0 + normal * barLen, "#804FC3F7", 1.2, true);
        // Bar Center (mid)
        AddGizmoLine(mid - normal * barLen * 1.2, mid + normal * barLen * 1.2, "#4FC3F7", 1.8, false);
        // Bar End (p1)
        AddGizmoLine(p1 - normal * barLen, p1 + normal * barLen, "#804FC3F7", 1.2, true);

        // Axis line
        AddGizmoLine(p0, p1, "#B0FFFFFF", 1.5, false);

        // Handles
        AddHandle(p0, MaskHandleKind.Start, "#FFFFFF", "#222222", 6.0);
        AddHandle(mid, MaskHandleKind.Center, "#4FC3F7", "#FFFFFF", 7.0);
        AddHandle(p1, MaskHandleKind.End, "#FFFFFF", "#222222", 6.0);
    }

    private void DrawRadialGizmo()
    {
        if (_gizmoMask == null) return;
        var p = _gizmoMask.MaskParams;
        double cx = ParseDouble(p, "cx", 0.5);
        double cy = ParseDouble(p, "cy", 0.5);
        double rx = ParseDouble(p, "rx", 0.25);
        double ry = ParseDouble(p, "ry", 0.25);
        double feather = ParseDouble(p, "feather", 0.4);

        Point center = NormToCanvas(new Point(cx, cy));
        Point right = NormToCanvas(new Point(cx + rx, cy));
        Point bottom = NormToCanvas(new Point(cx, cy + ry));

        double radiusX = Math.Max(10.0, Math.Abs(right.X - center.X));
        double radiusY = Math.Max(10.0, Math.Abs(bottom.Y - center.Y));

        // Feather outer ellipse
        double featherRx = radiusX * (1.0 + feather);
        double featherRy = radiusY * (1.0 + feather);
        var featherEllipse = new Ellipse
        {
            Width = featherRx * 2.0,
            Height = featherRy * 2.0,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#804FC3F7")),
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(featherEllipse, center.X - featherRx);
        Canvas.SetTop(featherEllipse, center.Y - featherRy);
        maskGizmoOverlay.Children.Add(featherEllipse);

        // Main inner ellipse
        var mainEllipse = new Ellipse
        {
            Width = radiusX * 2.0,
            Height = radiusY * 2.0,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FC3F7")),
            StrokeThickness = 1.8,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#154FC3F7")),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(mainEllipse, center.X - radiusX);
        Canvas.SetTop(mainEllipse, center.Y - radiusY);
        maskGizmoOverlay.Children.Add(mainEllipse);

        // 4 Scale/Resize Handles
        Point ptTop = new Point(center.X, center.Y - radiusY);
        Point ptBottom = new Point(center.X, center.Y + radiusY);
        Point ptLeft = new Point(center.X - radiusX, center.Y);
        Point ptRight = new Point(center.X + radiusX, center.Y);

        AddHandle(center, MaskHandleKind.Center, "#4FC3F7", "#FFFFFF", 7.0);
        AddHandle(ptTop, MaskHandleKind.Top, "#FFFFFF", "#222222", 5.0);
        AddHandle(ptBottom, MaskHandleKind.Bottom, "#FFFFFF", "#222222", 5.0);
        AddHandle(ptLeft, MaskHandleKind.Left, "#FFFFFF", "#222222", 5.0);
        AddHandle(ptRight, MaskHandleKind.Right, "#FFFFFF", "#222222", 5.0);
    }

    private void AddGizmoLine(Point a, Point b, string colorHex, double thickness, bool dashed)
    {
        var line = new Line
        {
            X1 = a.X, Y1 = a.Y,
            X2 = b.X, Y2 = b.Y,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            StrokeThickness = thickness,
            IsHitTestVisible = false
        };
        if (dashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
        maskGizmoOverlay.Children.Add(line);
    }

    private void AddHandle(Point pt, MaskHandleKind kind, string fillHex, string strokeHex, double radius)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2.0,
            Height = radius * 2.0,
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillHex)),
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(strokeHex)),
            StrokeThickness = 1.5,
            Tag = kind,
            Cursor = kind == MaskHandleKind.Center ? Cursors.SizeAll : Cursors.Hand
        };
        Canvas.SetLeft(ellipse, pt.X - radius);
        Canvas.SetTop(ellipse, pt.Y - radius);
        maskGizmoOverlay.Children.Add(ellipse);
    }

    private void MaskGizmo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_gizmoMask == null || e.LeftButton != MouseButtonState.Pressed) return;

        Point p = e.GetPosition(maskGizmoOverlay);
        _dragHandle = HitTestGizmoHandle(p);

        if (_dragHandle != MaskHandleKind.None)
        {
            _gizmoMouseStart = p;
            _gizmoInitParams = new Dictionary<string, string>(_gizmoMask.MaskParams);
            maskGizmoOverlay.CaptureMouse();
            e.Handled = true;
        }
    }

    private MaskHandleKind HitTestGizmoHandle(Point p)
    {
        // Prioritize tagged handle lookup
        foreach (UIElement child in maskGizmoOverlay.Children)
        {
            if (child is Ellipse el && el.Tag is MaskHandleKind kind)
            {
                double left = Canvas.GetLeft(el) + el.Width / 2.0;
                double top = Canvas.GetTop(el) + el.Height / 2.0;
                double dist = Math.Sqrt(Math.Pow(p.X - left, 2) + Math.Pow(p.Y - top, 2));
                if (dist <= 14.0) return kind;
            }
        }
        return MaskHandleKind.None;
    }

    private void MaskGizmo_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragHandle == MaskHandleKind.None || _gizmoMask == null) return;

        Point curr = e.GetPosition(maskGizmoOverlay);
        Point startNorm = CanvasToNorm(_gizmoMouseStart);
        Point currNorm = CanvasToNorm(curr);
        double deltaNormX = currNorm.X - startNorm.X;
        double deltaNormY = currNorm.Y - startNorm.Y;

        var init = _gizmoInitParams;

        if (_gizmoMask.MaskType == LinearGradientMask.Type)
        {
            double initX0 = ParseDouble(init, "x0", 0.5);
            double initY0 = ParseDouble(init, "y0", 0.1);
            double initX1 = ParseDouble(init, "x1", 0.5);
            double initY1 = ParseDouble(init, "y1", 0.6);

            switch (_dragHandle)
            {
                case MaskHandleKind.Center:
                    _gizmoMask.MaskParams["x0"] = Format(initX0 + deltaNormX);
                    _gizmoMask.MaskParams["y0"] = Format(initY0 + deltaNormY);
                    _gizmoMask.MaskParams["x1"] = Format(initX1 + deltaNormX);
                    _gizmoMask.MaskParams["y1"] = Format(initY1 + deltaNormY);
                    break;
                case MaskHandleKind.Start:
                    _gizmoMask.MaskParams["x0"] = Format(initX0 + deltaNormX);
                    _gizmoMask.MaskParams["y0"] = Format(initY0 + deltaNormY);
                    break;
                case MaskHandleKind.End:
                    _gizmoMask.MaskParams["x1"] = Format(initX1 + deltaNormX);
                    _gizmoMask.MaskParams["y1"] = Format(initY1 + deltaNormY);
                    break;
            }
        }
        else if (_gizmoMask.MaskType == RadialMask.Type)
        {
            double initCx = ParseDouble(init, "cx", 0.5);
            double initCy = ParseDouble(init, "cy", 0.5);
            double initRx = ParseDouble(init, "rx", 0.25);
            double initRy = ParseDouble(init, "ry", 0.25);

            switch (_dragHandle)
            {
                case MaskHandleKind.Center:
                    _gizmoMask.MaskParams["cx"] = Format(initCx + deltaNormX);
                    _gizmoMask.MaskParams["cy"] = Format(initCy + deltaNormY);
                    break;
                case MaskHandleKind.Left:
                case MaskHandleKind.Right:
                    _gizmoMask.MaskParams["rx"] = Format(Math.Max(0.02, Math.Abs(currNorm.X - initCx)));
                    break;
                case MaskHandleKind.Top:
                case MaskHandleKind.Bottom:
                    _gizmoMask.MaskParams["ry"] = Format(Math.Max(0.02, Math.Abs(currNorm.Y - initCy)));
                    break;
            }
        }

        RedrawMaskGizmo();
        _developPanel?.NotifyMaskParamsUpdated(_gizmoMask);
        e.Handled = true;
    }

    private void MaskGizmo_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragHandle != MaskHandleKind.None)
        {
            _dragHandle = MaskHandleKind.None;
            maskGizmoOverlay.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> dict, string key, double defaultVal)
    {
        return dict.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : defaultVal;
    }

    private static string Format(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
