using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Filters;

namespace ZVision.Imaging;

/// <summary>
/// Guided Filter: edge-preserving smoothing filter based on linear translation-variant filtering.
/// Transfers structural edges from the guidance image (or self-image) to smooth textures
/// while strictly preserving sharp high-contrast boundaries without gradient reversals or halo artifacts.
/// </summary>
public sealed class GuidedFilterOp : IEditOp
{
    public const string Type = "GuidedFilter";
    public string OpType => Type;

    /// <summary>Filter window radius at full resolution (e.g. 2..32, default 4).</summary>
    public float BaseRadius = 4f;

    /// <summary>Regularization parameter penalizing large gradients (eps, default 0.02f).</summary>
    public float Eps = 0.02f;

    /// <summary>Subsampling factor (1 = full res, 2 or 4 = accelerated Fast Guided Filter, default 1).</summary>
    public int Subsample = 1;

    /// <summary>Blend strength [0..1] between original and smoothed image (default 1.0f).</summary>
    public float Amount = 1.0f;

    public bool IsIdentity => Amount < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;

        int w = image.Width;
        int h = image.Height;
        int effRadius = Math.Max(1, (int)MathF.Round(BaseRadius * scale));
        float eps = Math.Max(1e-6f, Eps);
        int subsample = Math.Clamp(Subsample, 1, 4);
        float amt = Math.Clamp(Amount, 0f, 1f);

        if (amt >= 0.999f)
        {
            FastGuidedFilter.ApplyRgbaFloat(image.Pixels, image.Pixels, image.Pixels, w, h, effRadius, eps, subsample);
        }
        else
        {
            float[] filtered = new float[image.Pixels.Length];
            FastGuidedFilter.ApplyRgbaFloat(image.Pixels, image.Pixels, filtered, w, h, effRadius, eps, subsample);

            float invAmt = 1.0f - amt;
            Parallel.For(0, image.PixelCount, i =>
            {
                int p = i * 4;
                image.Pixels[p] = image.Pixels[p] * invAmt + filtered[p] * amt;
                image.Pixels[p + 1] = image.Pixels[p + 1] * invAmt + filtered[p + 1] * amt;
                image.Pixels[p + 2] = image.Pixels[p + 2] * invAmt + filtered[p + 2] * amt;
            });
        }
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["radius"] = BaseRadius.ToString("R", CultureInfo.InvariantCulture),
        ["eps"] = Eps.ToString("R", CultureInfo.InvariantCulture),
        ["subsample"] = Subsample.ToString(CultureInfo.InvariantCulture),
        ["amount"] = Amount.ToString("R", CultureInfo.InvariantCulture),
    };

    public static GuidedFilterOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        BaseRadius = EditOpRegistry.F(p, "radius", 4f),
        Eps = EditOpRegistry.F(p, "eps", 0.02f),
        Subsample = EditOpRegistry.I(p, "subsample", 1),
        Amount = EditOpRegistry.F(p, "amount", 1.0f),
    };

    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
