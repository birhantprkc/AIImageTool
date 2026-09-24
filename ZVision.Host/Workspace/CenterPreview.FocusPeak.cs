using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZVision.Host.Workspace;

// Focus Peaking (K key): highlights SHARP EDGES (high gradient) to verify focus area.
// Evaluated on preview image (Sobel on luminance) -> fast, syncs zoom/pan identically to clip overlay.
public partial class CenterPreview
{
    private bool _peakOverlay;

    private void TogglePeakOverlay()
    {
        if (imgPreview.Source is not BitmapSource bs) return;
        _peakOverlay = !_peakOverlay;
        if (_peakOverlay)
        {
            imgPeak.Source = BuildPeakMask(bs);
            SyncPeakTransform();
            imgPeak.Visibility = Visibility.Visible;
        }
        else
        {
            imgPeak.Visibility = Visibility.Collapsed;
            imgPeak.Source = null;
        }
    }

    private void RefreshPeakOverlayIfActive()
    {
        if (!_peakOverlay) return;
        if (imgPreview.Source is BitmapSource bs)
        {
            imgPeak.Source = BuildPeakMask(bs);
            SyncPeakTransform();
        }
    }

    private void SyncPeakTransform()
    {
        zoomScalePeak.ScaleX = zoomScale.ScaleX;
        zoomScalePeak.ScaleY = zoomScale.ScaleY;
        zoomPanPeak.X = zoomPan.X;
        zoomPanPeak.Y = zoomPan.Y;
    }

    /// <summary>
    /// Focus peaking mask: pixels with gradient magnitude (Sobel on luminance) exceeding threshold -> highlight
    /// (yellow-green), rest transparent. Adaptive threshold based on percentile.
    /// </summary>
    private static BitmapSource BuildPeakMask(BitmapSource src)
    {
        var fmt = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        fmt.CopyPixels(pixels, stride, 0);

        // Luminance 0..255.
        var lum = new float[w * h];
        for (int i = 0, p = 0; i < w * h; i++, p += 4)
            lum[i] = 0.114f * pixels[p] + 0.587f * pixels[p + 1] + 0.299f * pixels[p + 2];

        // Sobel magnitude.
        var mag = new float[w * h];
        float maxMag = 1e-6f;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                int c = y * w + x;
                float gx = (lum[c - w + 1] + 2f * lum[c + 1] + lum[c + w + 1])
                         - (lum[c - w - 1] + 2f * lum[c - 1] + lum[c + w - 1]);
                float gy = (lum[c + w - 1] + 2f * lum[c + w] + lum[c + w + 1])
                         - (lum[c - w - 1] + 2f * lum[c - w] + lum[c - w + 1]);
                float m = MathF.Sqrt(gx * gx + gy * gy);
                mag[c] = m;
                if (m > maxMag) maxMag = m;
            }

        // Threshold: strongest edges (~ top gradient region). Ratio of max + floor.
        float threshold = MathF.Max(40f, maxMag * 0.35f);

        var outPx = new byte[stride * h];
        for (int i = 0, p = 0; i < w * h; i++, p += 4)
        {
            if (mag[i] >= threshold)
            {
                // High-visibility yellow-green (camera focus peaking style).
                outPx[p] = 30; outPx[p + 1] = 255; outPx[p + 2] = 230; outPx[p + 3] = 230;
            }
            // remaining set to 0 (transparent).
        }

        var wb = new WriteableBitmap(w, h, src.DpiX, src.DpiY, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), outPx, stride, 0);
        wb.Freeze();
        return wb;
    }
}
