using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

// Side-by-side before/after compare (11.4).
public partial class CenterPreview
{
    private bool _compareMode;
    private double _cmpZoom = 1.0;
    private bool _isCmpPanning;
    private Point _cmpPanStartMouse;
    private double _cmpPanStartXBefore;
    private double _cmpPanStartYBefore;

    private void BtnCompare_Click(object sender, RoutedEventArgs e) => ToggleCompareMode();

    /// <summary>Synchronized zoom across 2 compare panes (8.6) — wheel zooms both simultaneously.</summary>
    private void PaneCompare_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!_compareMode) return;
        double old = _cmpZoom;
        double factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
        _cmpZoom = Math.Clamp(_cmpZoom * factor, 1.0, 8.0);
        if (Math.Abs(_cmpZoom - old) < 1e-6) return;

        cmpScaleBefore.ScaleX = cmpScaleBefore.ScaleY = _cmpZoom;
        cmpScaleAfter.ScaleX = cmpScaleAfter.ScaleY = _cmpZoom;

        // Zoom centered on cursor position
        var mousePos = e.GetPosition(paneCompare);
        bool isAfterSide = mousePos.X > paneCompare.ActualWidth / 2;
        var p = isAfterSide ? e.GetPosition(imgCompareAfter) : e.GetPosition(imgCompareBefore);

        double scaleRatio = _cmpZoom / old;
        cmpPanBefore.X = (cmpPanBefore.X - p.X) * scaleRatio + p.X;
        cmpPanBefore.Y = (cmpPanBefore.Y - p.Y) * scaleRatio + p.Y;

        ClampComparePan();
        e.Handled = true;
    }

    private void PaneCompare_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_compareMode || _cmpZoom <= 1.01) return;
        _isCmpPanning = true;
        _cmpPanStartMouse = e.GetPosition(paneCompare);
        _cmpPanStartXBefore = cmpPanBefore.X;
        _cmpPanStartYBefore = cmpPanBefore.Y;
        paneCompare.CaptureMouse();
        paneCompare.Cursor = System.Windows.Input.Cursors.ScrollAll;
        e.Handled = true;
    }

    private void PaneCompare_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_compareMode || !_isCmpPanning) return;
        var pos = e.GetPosition(paneCompare);
        double dx = pos.X - _cmpPanStartMouse.X;
        double dy = pos.Y - _cmpPanStartMouse.Y;

        cmpPanBefore.X = _cmpPanStartXBefore + dx;
        cmpPanBefore.Y = _cmpPanStartYBefore + dy;

        ClampComparePan();
        e.Handled = true;
    }

    private void PaneCompare_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isCmpPanning)
        {
            _isCmpPanning = false;
            paneCompare.ReleaseMouseCapture();
            paneCompare.Cursor = System.Windows.Input.Cursors.Arrow;
            e.Handled = true;
        }
    }

    private void PaneCompare_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isCmpPanning)
        {
            _isCmpPanning = false;
            paneCompare.ReleaseMouseCapture();
            paneCompare.Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void ClampComparePan()
    {
        double w = imgCompareBefore.ActualWidth;
        double h = imgCompareBefore.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double maxX = w * (_cmpZoom - 1);
        double maxY = h * (_cmpZoom - 1);

        cmpPanBefore.X = Math.Clamp(cmpPanBefore.X, -maxX, 0);
        cmpPanBefore.Y = Math.Clamp(cmpPanBefore.Y, -maxY, 0);

        // Synchronize completely to After pane
        cmpPanAfter.X = cmpPanBefore.X;
        cmpPanAfter.Y = cmpPanBefore.Y;
    }

    private void ResetCompareZoom()
    {
        _cmpZoom = 1.0;
        cmpScaleBefore.ScaleX = cmpScaleBefore.ScaleY = 1.0;
        cmpScaleAfter.ScaleX = cmpScaleAfter.ScaleY = 1.0;
        cmpPanBefore.X = cmpPanBefore.Y = 0;
        cmpPanAfter.X = cmpPanAfter.Y = 0;
    }

    private void ToggleCompareMode()
    {
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path)) return;

        _compareMode = !_compareMode;
        if (_compareMode)
        {
            // Disable crop if active to avoid overlay conflict.
            if (_cropMode) ToggleCropMode();
            paneSingle.Visibility = Visibility.Collapsed;
            paneGrid.Visibility = Visibility.Collapsed;
            paneCull.Visibility = Visibility.Collapsed;
            paneFull.Visibility = Visibility.Collapsed;
            paneCompare.Visibility = Visibility.Visible;
            btnCompare.Background = ThemeManager.GetBrush("AccentBrush");
            ResetCompareZoom();
            _ = LoadCompareAsync(path);
        }
        else
        {
            paneCompare.Visibility = Visibility.Collapsed;
            imgCompareBefore.Source = null;
            imgCompareAfter.Source = null;
            btnCompare.Background = ThemeManager.GetBrush("BgHoverBrush");
            SwitchMode(LighttableMode.Single);
        }
    }

    /// <summary>Render original image (pointer=0) and edited image (current pointer) into 2 panes.</summary>
    private async System.Threading.Tasks.Task LoadCompareAsync(string path)
    {
        // AFTER: edited image. BEFORE: original image.
        var ops = _history?.GetStack(path) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
        int pointer = _history?.GetPointer(path) ?? 0;

        if (_renderer.CanDecode(path) && pointer > 0)
        {
            try
            {
                var before = await _renderer.RenderPreviewAsync(path, ops, 0);
                var after = await _renderer.RenderPreviewAsync(path, ops, pointer);
                if (!_compareMode) return;
                if (before != null) imgCompareBefore.Source = before;
                if (after != null) imgCompareAfter.Source = after;
                return;
            }
            catch { }
        }

        // Fallback: unedited or undecodable image -> both panes show original file from disk.
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 1600;
            bmp.EndInit();
            bmp.Freeze();
            imgCompareBefore.Source = bmp;
            imgCompareAfter.Source = bmp;
        }
        catch { }
    }
}
