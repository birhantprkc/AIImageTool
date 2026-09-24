using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZVision.Imaging;

namespace ZVision.Host.Workspace;

/// <summary>
/// Ansel Adams 11-Zone System Exposure Visualizer.
/// Calibrated false-color mapping based on stops relative to 18% Middle Gray (Zone V).
/// </summary>
public partial class CenterPreview
{
    private bool _zoneOverlayActive;

    /// <summary>
    /// Gets whether the Ansel Adams Zone System overlay is currently visible.
    /// </summary>
    public bool IsZoneOverlayActive => _zoneOverlayActive;

    /// <summary>
    /// Toolbar button click handler to toggle Zone System visualizer.
    /// </summary>
    private void BtnZone_Click(object sender, RoutedEventArgs e) => ToggleZoneSystemOverlay();

    /// <summary>
    /// Toggles the Ansel Adams Zone System false-color visualizer on / off.
    /// </summary>
    public void ToggleZoneSystemOverlay()
    {
        if (imgPreview.Source is not BitmapSource bs) return;
        _zoneOverlayActive = !_zoneOverlayActive;

        if (_zoneOverlayActive)
        {
            imgZone.Source = BuildZoneSystemMask(bs);
            SyncZoneTransform();
            imgZone.Visibility = Visibility.Visible;
            if (panelZoneHud != null) panelZoneHud.Visibility = Visibility.Visible;
            if (btnZone != null) btnZone.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "AccentBrush");
        }
        else
        {
            imgZone.Visibility = Visibility.Collapsed;
            imgZone.Source = null;
            if (panelZoneHud != null) panelZoneHud.Visibility = Visibility.Collapsed;
            if (btnZone != null) btnZone.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
        }
    }

    /// <summary>
    /// Updates Zone System overlay if active when preview bitmap changes.
    /// </summary>
    private void RefreshZoneOverlayIfActive()
    {
        if (!_zoneOverlayActive) return;
        if (imgPreview.Source is BitmapSource bs)
        {
            imgZone.Source = BuildZoneSystemMask(bs);
            SyncZoneTransform();
        }
    }

    /// <summary>
    /// Synchronizes the Zone overlay's transform with main preview zoom and pan.
    /// </summary>
    private void SyncZoneTransform()
    {
        if (zoomScaleZone != null)
        {
            zoomScaleZone.ScaleX = zoomScale.ScaleX;
            zoomScaleZone.ScaleY = zoomScale.ScaleY;
        }
        if (zoomPanZone != null)
        {
            zoomPanZone.X = zoomPan.X;
            zoomPanZone.Y = zoomPan.Y;
        }
    }

    /// <summary>
    /// Generates false-color Zone System overlay from the rendered source bitmap.
    /// </summary>
    public static BitmapSource BuildZoneSystemMask(BitmapSource src)
    {
        var fmt = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        fmt.CopyPixels(pixels, stride, 0);

        ZoneSystem.MapBgraPixelsToZoneMask(pixels);

        var res = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        res.Freeze();
        return res;
    }
}
