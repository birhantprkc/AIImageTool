using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroVision.Imaging;

namespace ZeroVision.Host.Workspace;

// Soft Proofing & Gamut Warning overlay: S key toggles. Highlights out-of-gamut pixels in vivid warning overlay.
public partial class CenterPreview
{
    private bool _proofOverlay;
    private ColorSpaces.Space _proofSpace = ColorSpaces.Space.Srgb;

    public bool IsProofOverlayActive => _proofOverlay;

    private void BtnProof_Click(object sender, RoutedEventArgs e) => ToggleGamutWarningOverlay();

    public void ToggleGamutWarningOverlay()
    {
        if (imgPreview.Source is not BitmapSource bs) return;
        _proofOverlay = !_proofOverlay;
        if (_proofOverlay)
        {
            imgProof.Source = BuildProofMask(bs, _proofSpace);
            SyncProofTransform();
            imgProof.Visibility = Visibility.Visible;
            if (btnProof != null) btnProof.Background = ThemeManager.GetBrush("AccentBrush");
        }
        else
        {
            imgProof.Visibility = Visibility.Collapsed;
            imgProof.Source = null;
            if (btnProof != null) btnProof.Background = ThemeManager.GetBrush("BgHoverBrush");
        }
    }

    /// <summary>Update gamut warning overlay when preview image changes (if enabled).</summary>
    private void RefreshProofOverlayIfActive()
    {
        if (!_proofOverlay) return;
        if (imgPreview.Source is BitmapSource bs)
        {
            imgProof.Source = BuildProofMask(bs, _proofSpace);
            SyncProofTransform();
        }
    }

    /// <summary>Synchronize proof overlay transform with preview image (zoom/pan).</summary>
    private void SyncProofTransform()
    {
        zoomScaleProof.ScaleX = zoomScale.ScaleX;
        zoomScaleProof.ScaleY = zoomScale.ScaleY;
        zoomPanProof.X = zoomPan.X;
        zoomPanProof.Y = zoomPan.Y;
    }

    /// <summary>
    /// Checks pixels using GamutCheck.IsOutOfGamut against destination color space (e.g. sRGB).
    /// Out-of-gamut pixels rendered in vivid cyan warning color (#D000E5FF),
    /// inside-gamut pixels 100% transparent.
    /// </summary>
    private static BitmapSource BuildProofMask(BitmapSource src, ColorSpaces.Space dest)
    {
        var fmt = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        fmt.CopyPixels(pixels, stride, 0);

        float[] destToXyz = ColorSpaces.RgbToXyzD65(dest);
        float[] srgbToXyz = ColorSpaces.RgbToXyzD65(ColorSpaces.Space.Srgb);
        float[] toDest = ColorSpaces.Mul3x3(ColorSpaces.Invert3x3(destToXyz), srgbToXyz);
        float a0 = toDest[0], a1 = toDest[1], a2 = toDest[2];
        float a3 = toDest[3], a4 = toDest[4], a5 = toDest[5];
        float a6 = toDest[6], a7 = toDest[7], a8 = toDest[8];

        const float tol = 1e-4f;
        // High visibility cyan/red warning color for soft-proofing gamut alerts (Lightroom standard)
        const byte warnB = 255, warnG = 60, warnR = 20, warnA = 210;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            float bLin = ColorSpace.SrgbToLinear(pixels[i] / 255f);
            float gLin = ColorSpace.SrgbToLinear(pixels[i + 1] / 255f);
            float rLin = ColorSpace.SrgbToLinear(pixels[i + 2] / 255f);

            float dr = a0 * rLin + a1 * gLin + a2 * bLin;
            float dg = a3 * rLin + a4 * gLin + a5 * bLin;
            float db = a6 * rLin + a7 * gLin + a8 * bLin;

            bool outOfGamut = dr < -tol || dg < -tol || db < -tol || dr > 1f + tol || dg > 1f + tol || db > 1f + tol;
            if (outOfGamut)
            {
                pixels[i] = warnB;
                pixels[i + 1] = warnG;
                pixels[i + 2] = warnR;
                pixels[i + 3] = warnA;
            }
            else
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 0;
            }
        }

        var wb = new WriteableBitmap(w, h, src.DpiX, src.DpiY, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        wb.Freeze();
        return wb;
    }
}
