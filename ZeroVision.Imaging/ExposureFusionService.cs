using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Fusion;

namespace ZeroVision.Imaging;

/// <summary>
/// Computational Photography Multi-Frame Fusion Service.
/// Delegates high-dynamic-range bracket exposure fusion and extended depth-of-field focus stacking
/// directly to the accelerated Burt-Adelson multi-scale pyramid engine in ZeroGraphics.
/// </summary>
public static class ExposureFusionService
{
    /// <summary>
    /// Fuses a bracketed exposure sequence into a high-dynamic-range composite using Mertens Exposure Fusion.
    /// Eliminates blown highlights and recovers shadow details without tone-mapping artifacts.
    /// </summary>
    public static LinearImage FuseExposures(
        IReadOnlyList<LinearImage> images,
        float wContrast = 1.0f,
        float wSaturation = 1.0f,
        float wExposedness = 1.0f,
        int maxLevels = 6)
    {
        if (images == null || images.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(images));

        if (images.Count == 1)
            return images[0].Clone();

        int w = images[0].Width;
        int h = images[0].Height;

        var floatArrays = new List<float[]>(images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            var img = images[i];
            if (img.Width != w || img.Height != h)
                throw new ArgumentException($"Image {i} dimensions ({img.Width}x{img.Height}) do not match stack ({w}x{h}).");

            floatArrays.Add(img.Pixels);
        }

        float[] fusedPixels = MertensExposureFusion.FuseRgbaFloat(
            floatArrays,
            w,
            h,
            wContrast,
            wSaturation,
            wExposedness,
            maxLevels);

        return new LinearImage(w, h, fusedPixels);
    }

    /// <summary>
    /// Fuses a focus-bracketed sequence into an extended depth-of-field composite using multi-scale Laplacian pyramid focus stacking.
    /// Combines the sharpest optical planes across all frequency bands without boundary seams or halos.
    /// </summary>
    public static LinearImage FuseFocusStack(
        IReadOnlyList<LinearImage> images,
        int maxLevels = 6,
        float sharpnessPower = 8.0f)
    {
        if (images == null || images.Count == 0)
            throw new ArgumentException("At least one image is required.", nameof(images));

        if (images.Count == 1)
            return images[0].Clone();

        int w = images[0].Width;
        int h = images[0].Height;

        var floatArrays = new List<float[]>(images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            var img = images[i];
            if (img.Width != w || img.Height != h)
                throw new ArgumentException($"Image {i} dimensions ({img.Width}x{img.Height}) do not match stack ({w}x{h}).");

            floatArrays.Add(img.Pixels);
        }

        float[] fusedPixels = PyramidFocusStacking.FuseRgbaFloat(
            floatArrays,
            w,
            h,
            maxLevels,
            sharpnessPower);

        return new LinearImage(w, h, fusedPixels);
    }
}
