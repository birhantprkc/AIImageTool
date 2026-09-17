using ZeroGraphics.Imaging.ColorScience;

namespace ZeroVision.Imaging;

/// <summary>
/// Photographic perceptual OKLab color conversions delegating to ZeroGraphics.Imaging.ColorScience.OklabColor.
/// </summary>
public static class OklabColor
{
    public static void LinearRgbToOklab(float r, float g, float b, out float L, out float a, out float bOut)
        => ZeroGraphics.Imaging.ColorScience.OklabColor.LinearRgbToOklab(r, g, b, out L, out a, out bOut);

    public static void OklabToLinearRgb(float L, float a, float bIn, out float r, out float g, out float b)
        => ZeroGraphics.Imaging.ColorScience.OklabColor.OklabToLinearRgb(L, a, bIn, out r, out g, out b);

    public static bool IsInGamut(float r, float g, float b, float tol = 1e-4f)
        => ZeroGraphics.Imaging.ColorScience.OklabColor.IsInGamut(r, g, b, tol);

    public static void CompressToGamut(ref float L, ref float a, ref float bIn)
        => ZeroGraphics.Imaging.ColorScience.OklabColor.CompressToGamut(ref L, ref a, ref bIn);
}
