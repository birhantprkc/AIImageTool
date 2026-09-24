using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZVision.Host.Workspace;

// Clipping overlay (13.9): J key toggles. Red = highlight clipping (>=250), blue = shadow clipping (<=5).
public partial class CenterPreview
{
    private bool _clipOverlay;

    private void ToggleClipOverlay()
    {
        if (imgPreview.Source is not BitmapSource bs) return;
        _clipOverlay = !_clipOverlay;
        if (_clipOverlay)
        {
            imgClip.Source = BuildClipMask(bs);
            SyncClipTransform();
            imgClip.Visibility = Visibility.Visible;
        }
        else
        {
            imgClip.Visibility = Visibility.Collapsed;
            imgClip.Source = null;
        }
    }

    /// <summary>Update overlay when preview image changes (if enabled).</summary>
    private void RefreshClipOverlayIfActive()
    {
        if (!_clipOverlay) return;
        if (imgPreview.Source is BitmapSource bs)
        {
            imgClip.Source = BuildClipMask(bs);
            SyncClipTransform();
        }
    }

    /// <summary>Synchronize overlay transform with preview image (zoom/pan).</summary>
    private void SyncClipTransform()
    {
        zoomScaleClip.ScaleX = zoomScale.ScaleX;
        zoomScaleClip.ScaleY = zoomScale.ScaleY;
        zoomPanClip.X = zoomPan.X;
        zoomPanClip.Y = zoomPan.Y;
    }

    /// <summary>
    /// Generate transparent mask image: clipped highlights -> solid red, crushed shadows -> solid blue,
    /// remaining pixels transparent. Overlaid onto preview sharing identical transform.
    /// </summary>
    private static BitmapSource BuildClipMask(BitmapSource src)
    {
        var fmt = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        fmt.CopyPixels(pixels, stride, 0);

        const byte hi = 250, lo = 5;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            bool blown = r >= hi && g >= hi && b >= hi;
            bool crushed = r <= lo && g <= lo && b <= lo;
            if (blown) { pixels[i] = 60; pixels[i + 1] = 60; pixels[i + 2] = 255; pixels[i + 3] = 200; }
            else if (crushed) { pixels[i] = 255; pixels[i + 1] = 90; pixels[i + 2] = 60; pixels[i + 3] = 200; }
            else { pixels[i] = 0; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 0; }
        }

        var wb = new WriteableBitmap(w, h, src.DpiX, src.DpiY, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        wb.Freeze();
        return wb;
    }

    /// <summary>Temporarily toggle clipping preview (for Alt-drag slider QoL preview).</summary>
    public void SetTemporaryClipOverlay(bool active)
    {
        if (imgPreview.Source is not BitmapSource bs) return;
        if (active)
        {
            imgClip.Source = BuildClipMask(bs);
            SyncClipTransform();
            imgClip.Visibility = Visibility.Visible;
        }
        else
        {
            if (!_clipOverlay) // Only hide if user has not permanently toggled J
            {
                imgClip.Visibility = Visibility.Collapsed;
                imgClip.Source = null;
            }
        }
    }
}
