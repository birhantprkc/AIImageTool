using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Filters;

namespace ZVision.Imaging;

/// <summary>
/// Directed Median Op: edge-preserving 4-directional median filter.
/// Evaluates local ray variance along 4 orientations (0, 45, 90, 135 degrees) to find the principal edge axis,
/// then performs a 1D median filter along that direction. Eliminates impulse/salt-and-pepper noise and hot/dead pixels
/// without blunting sharp corners or cutting thin 1-pixel lines.
/// </summary>
public sealed class DirectedMedianOp : IEditOp
{
    public const string Type = "DirectedMedian";
    public string OpType => Type;

    /// <summary>Impulse detection threshold (default 0.05f).</summary>
    public float Threshold = 0.05f;

    /// <summary>Blend strength [0..1] (default 1.0f).</summary>
    public float Strength = 1.0f;

    public bool IsIdentity => Strength < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;

        int w = image.Width;
        int h = image.Height;
        if (w < 3 || h < 3) return;

        float thr = Math.Max(1e-4f, Threshold);
        float strength = Math.Clamp(Strength, 0f, 1f);

        if (strength >= 0.999f)
        {
            DirectedMedianFilter.ApplyRgbaFloat(image.Pixels, w, h, thr);
        }
        else
        {
            float[] filtered = (float[])image.Pixels.Clone();
            DirectedMedianFilter.ApplyRgbaFloat(filtered, w, h, thr);

            float invStr = 1.0f - strength;
            Parallel.For(0, image.PixelCount, i =>
            {
                int p = i * 4;
                image.Pixels[p] = image.Pixels[p] * invStr + filtered[p] * strength;
                image.Pixels[p + 1] = image.Pixels[p + 1] * invStr + filtered[p + 1] * strength;
                image.Pixels[p + 2] = image.Pixels[p + 2] * invStr + filtered[p + 2] * strength;
            });
        }
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["threshold"] = Threshold.ToString("R", CultureInfo.InvariantCulture),
        ["strength"] = Strength.ToString("R", CultureInfo.InvariantCulture),
    };

    public static DirectedMedianOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        Threshold = EditOpRegistry.F(p, "threshold", 0.05f),
        Strength = EditOpRegistry.F(p, "strength", 1.0f),
    };

    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
