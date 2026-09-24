using System;
using System.Collections.Generic;
using System.Globalization;
using ZeroGraphics.Imaging.Filters;

namespace ZVision.Imaging;

/// <summary>
/// Wavelet Denoising Op: multiscale stationary À Trous B-spline wavelet filter.
/// Provides shift-invariant, scale-dependent shrinkage separating high-frequency sensor noise
/// and chroma grain from structural edges and fine textures without edge blurring or ringing artifacts.
/// </summary>
public sealed class WaveletDenoiseOp : IEditOp
{
    public const string Type = "WaveletDenoise";
    public string OpType => Type;

    /// <summary>Luminance noise reduction strength [0..1] (default 0.5f).</summary>
    public float LumaAmount = 0.5f;

    /// <summary>Chrominance noise reduction strength [0..1] (default 0.8f).</summary>
    public float ChromaAmount = 0.8f;

    /// <summary>Number of dyadic wavelet decomposition scales [1..6] (default 4).</summary>
    public int Scales = 4;

    public bool IsIdentity => LumaAmount < 1e-4f && ChromaAmount < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;

        int w = image.Width;
        int h = image.Height;
        if (w < 4 || h < 4) return;

        float luma = Math.Clamp(LumaAmount, 0f, 1f);
        float chroma = Math.Clamp(ChromaAmount, 0f, 1f);
        int numScales = Math.Clamp(Scales, 1, 6);

        AtrousWaveletFilter.ApplyRgbaFloat(image.Pixels, w, h, luma, chroma, numScales);
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["luma"] = LumaAmount.ToString("R", CultureInfo.InvariantCulture),
        ["chroma"] = ChromaAmount.ToString("R", CultureInfo.InvariantCulture),
        ["scales"] = Scales.ToString(CultureInfo.InvariantCulture),
    };

    public static WaveletDenoiseOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        LumaAmount = EditOpRegistry.F(p, "luma", 0.5f),
        ChromaAmount = EditOpRegistry.F(p, "chroma", 0.8f),
        Scales = EditOpRegistry.I(p, "scales", 4),
    };

    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
