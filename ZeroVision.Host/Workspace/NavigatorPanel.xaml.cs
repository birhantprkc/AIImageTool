using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ZeroVision.Host.Workspace;

public partial class NavigatorPanel : UserControl
{
    private CenterPreview? _centerPreview;
    private bool _isDragging;

    public NavigatorPanel()
    {
        InitializeComponent();
        zoomSwitcher.Items = new[] { "FIT", "FILL", "1:1", "2:1" };
        zoomSwitcher.SelectedIndex = 0;
        zoomSwitcher.SelectedIndexChanged += ZoomSwitcher_SelectedIndexChanged;
        navHost.SizeChanged += (_, _) => RedrawViewport();
    }

    private void ZoomSwitcher_SelectedIndexChanged(object? sender, int index)
    {
        if (_centerPreview == null || index < 0) return;
        string mode = index switch
        {
            0 => "FIT",
            1 => "FILL",
            2 => "1:1",
            3 => "2:1",
            _ => "FIT"
        };
        _centerPreview.ZoomToMode(mode);
    }

    public void BindCenterPreview(CenterPreview centerPreview)
    {
        if (_centerPreview != null)
        {
            _centerPreview.ViewportChanged -= OnViewportChanged;
        }
        _centerPreview = centerPreview;
        if (_centerPreview != null)
        {
            _centerPreview.ViewportChanged += OnViewportChanged;
        }
    }

    private double _normX, _normY, _normW = 1.0, _normH = 1.0;
    private double _currentZoom = 1.0;

    private void OnViewportChanged(object? sender, ViewportChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            imgThumb.Source = e.Source;
            txtEmpty.Visibility = e.Source != null ? Visibility.Collapsed : Visibility.Visible;

            _currentZoom = e.Zoom;
            _normX = e.NormX;
            _normY = e.NormY;
            _normW = e.NormW;
            _normH = e.NormH;

            int targetIndex = e.Zoom switch
            {
                <= 1.001 => 0,
                _ when Math.Abs(e.Zoom - 2.0) < 0.05 => 3,
                _ => 2
            };
            if (zoomSwitcher.SelectedIndex != targetIndex)
            {
                zoomSwitcher.SelectedIndexChanged -= ZoomSwitcher_SelectedIndexChanged;
                zoomSwitcher.SelectedIndex = targetIndex;
                zoomSwitcher.SelectedIndexChanged += ZoomSwitcher_SelectedIndexChanged;
            }

            RedrawViewport();
        });
    }

    private void RedrawViewport()
    {
        if (imgThumb.Source == null || navHost.ActualWidth <= 0 || navHost.ActualHeight <= 0)
        {
            rectViewport.Visibility = Visibility.Collapsed;
            return;
        }

        double hostW = navHost.ActualWidth;
        double hostH = navHost.ActualHeight;
        double imgW = imgThumb.Source.Width;
        double imgH = imgThumb.Source.Height;
        if (imgW <= 0 || imgH <= 0) return;

        double scale = Math.Min(hostW / imgW, hostH / imgH);
        double renderW = imgW * scale;
        double renderH = imgH * scale;
        double offsetX = (hostW - renderW) / 2;
        double offsetY = (hostH - renderH) / 2;

        if (_currentZoom <= 1.001)
        {
            rectViewport.Visibility = Visibility.Collapsed;
            return;
        }

        rectViewport.Visibility = Visibility.Visible;
        double vx = offsetX + Math.Clamp(_normX, 0, 1) * renderW;
        double vy = offsetY + Math.Clamp(_normY, 0, 1) * renderH;
        double vw = Math.Clamp(_normW * renderW, 8, renderW);
        double vh = Math.Clamp(_normH * renderH, 8, renderH);

        rectViewport.Width = vw;
        rectViewport.Height = vh;
        Canvas.SetLeft(rectViewport, vx);
        Canvas.SetTop(rectViewport, vy);
    }

    private void Navigator_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            (sender as UIElement)?.CaptureMouse();
            PanFromMouse(e.GetPosition(navHost));
        }
    }

    private void Navigator_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            PanFromMouse(e.GetPosition(navHost));
        }
    }

    private void Navigator_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }
    }

    private void PanFromMouse(Point p)
    {
        if (_centerPreview == null || imgThumb.Source == null) return;
        double hostW = navHost.ActualWidth;
        double hostH = navHost.ActualHeight;
        double imgW = imgThumb.Source.Width;
        double imgH = imgThumb.Source.Height;
        if (imgW <= 0 || imgH <= 0 || hostW <= 0 || hostH <= 0) return;

        double scale = Math.Min(hostW / imgW, hostH / imgH);
        double renderW = imgW * scale;
        double renderH = imgH * scale;
        double offsetX = (hostW - renderW) / 2;
        double offsetY = (hostH - renderH) / 2;

        double relX = (p.X - offsetX) / renderW;
        double relY = (p.Y - offsetY) / renderH;

        _centerPreview.PanToNormalized(Math.Clamp(relX, 0, 1), Math.Clamp(relY, 0, 1));
    }
}
