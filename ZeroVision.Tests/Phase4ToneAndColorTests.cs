using System;
using System.Collections.Generic;
using Xunit;
using ZeroVision.Imaging;

namespace ZeroVision.Tests;

public class Phase4ToneAndColorTests
{
    [Theory]
    [InlineData(0.0f, 0.0f, 0.0f)]       // Black
    [InlineData(1.0f, 1.0f, 1.0f)]       // White
    [InlineData(0.18f, 0.18f, 0.18f)]   // Middle gray
    [InlineData(1.0f, 0.0f, 0.0f)]       // Red
    [InlineData(0.0f, 1.0f, 0.0f)]       // Green
    [InlineData(0.0f, 0.0f, 1.0f)]       // Blue
    [InlineData(0.8f, 0.6f, 0.4f)]       // Skin / warm tone
    public void Oklab_RoundTrip_Invertible(float r, float g, float b)
    {
        OklabColor.LinearRgbToOklab(r, g, b, out float L, out float a, out float okB);
        OklabColor.OklabToLinearRgb(L, a, okB, out float rRec, out float gRec, out float bRec);

        Assert.InRange(rRec, r - 1e-4f, r + 1e-4f);
        Assert.InRange(gRec, g - 1e-4f, g + 1e-4f);
        Assert.InRange(bRec, b - 1e-4f, b + 1e-4f);
    }

    [Theory]
    [InlineData(0.95f, 0.3f, 0.2f)]      // Out of gamut bright saturated red/orange
    [InlineData(0.85f, -0.4f, 0.3f)]     // Out of gamut bright green/cyan
    [InlineData(0.90f, 0.1f, -0.35f)]    // Out of gamut bright blue/magenta
    public void Oklab_GamutCompression_PreservesHue(float inL, float inA, float inB)
    {
        float L = inL;
        float a = inA;
        float b = inB;

        float originalHue = MathF.Atan2(inB, inA);

        OklabColor.CompressToGamut(ref L, ref a, ref b);

        OklabColor.OklabToLinearRgb(L, a, b, out float r, out float g, out float bOut);

        // Resulting RGB must fit inside [0..1] gamut
        Assert.True(OklabColor.IsInGamut(r, g, bOut, 1e-3f));

        // Hue angle must remain constant (within tolerance for non-zero chroma)
        float compressedChroma = MathF.Sqrt(a * a + b * b);
        if (compressedChroma > 1e-3f)
        {
            float newHue = MathF.Atan2(b, a);
            Assert.Equal(originalHue, newHue, 3);
        }
    }

    [Fact]
    public void ToneCurveOp_PerceptualOklab_AppliesSmoothly()
    {
        // Simple S-curve
        var points = new List<(float x, float y)>
        {
            (0.0f, 0.0f),
            (0.25f, 0.18f),
            (0.75f, 0.82f),
            (1.0f, 1.0f)
        };

        var op = new ToneCurveOp(rgb: points)
        {
            HueMode = ToneCurveHueMode.PerceptualOklab
        };

        var img = new LinearImage(4, 4);
        img.ProcessPixels((ref float r, ref float g, ref float b, ref float a) =>
        {
            r = 0.5f; g = 0.4f; b = 0.3f; a = 1.0f;
        });

        op.Apply(img, 1.0f);

        // All pixels should remain strictly positive and well-formed
        img.ProcessPixels((ref float r, ref float g, ref float b, ref float a) =>
        {
            Assert.True(r > 0f && r <= 1f);
            Assert.True(g > 0f && g <= 1f);
            Assert.True(b > 0f && b <= 1f);
        });

        // Verify parameter persistence
        var dict = op.ToParams();
        Assert.Equal("PerceptualOklab", dict["hueMode"]);
        var restored = ToneCurveOp.FromParams(dict);
        Assert.Equal(ToneCurveHueMode.PerceptualOklab, restored.HueMode);
        Assert.True(restored.PreserveHue);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void LutCubeOp_Tetrahedral_PreservesNeutralDiagonal(float gray)
    {
        // Construct 2x2x2 identity LUT (size=2)
        int size = 2;
        var lut = new float[size * size * size * 3];
        for (int b = 0; b < size; b++)
        for (int g = 0; g < size; g++)
        for (int r = 0; r < size; r++)
        {
            int idx = ((b * size + g) * size + r) * 3;
            lut[idx] = r / (float)(size - 1);
            lut[idx + 1] = g / (float)(size - 1);
            lut[idx + 2] = b / (float)(size - 1);
        }

        LutCubeOp.Tetrahedral(lut, size, gray, gray, gray, out float or, out float og, out float ob);

        Assert.InRange(or, gray - 1e-4f, gray + 1e-4f);
        Assert.InRange(og, gray - 1e-4f, gray + 1e-4f);
        Assert.InRange(ob, gray - 1e-4f, gray + 1e-4f);
    }

    [Fact]
    public void ZoneSystem_ClassificationAndMask_MatchesAnselAdamsZones()
    {
        // Middle gray linear (0.18) must map to Zone V (index 5)
        int midZone = ZoneSystem.GetZoneIndex(0.18f);
        Assert.Equal(5, midZone);

        // Near black (e.g. 0.001 linear) must map to Zone 0
        int blackZone = ZoneSystem.GetZoneIndex(0.001f);
        Assert.Equal(0, blackZone);

        // White (1.0 linear) must map to Zone X (index 10)
        int whiteZone = ZoneSystem.GetZoneIndex(1.0f);
        Assert.Equal(10, whiteZone);

        // Test MapBgraPixelsToZoneMask on a 4-pixel buffer:
        // Pixel 0: Black (0, 0, 0) -> Zone 0 (B=40, G=0, R=16)
        // Pixel 1: Middle Gray (118, 118, 118) -> Zone V (B=158, G=158, R=158)
        // Pixel 2: Highlight (215, 215, 215) -> Zone VIII (B=53, G=57, R=229)
        // Pixel 3: White (255, 255, 255) -> Zone X (B=255, G=255, R=255)
        var pixels = new byte[]
        {
            0, 0, 0, 255,
            118, 118, 118, 255,
            215, 215, 215, 255,
            255, 255, 255, 255
        };

        ZoneSystem.MapBgraPixelsToZoneMask(pixels);

        // Pixel 0
        Assert.Equal(40, pixels[0]);
        Assert.Equal(0, pixels[1]);
        Assert.Equal(16, pixels[2]);

        // Pixel 1
        Assert.Equal(158, pixels[4]);
        Assert.Equal(158, pixels[5]);
        Assert.Equal(158, pixels[6]);

        // Pixel 3
        Assert.Equal(255, pixels[12]);
        Assert.Equal(255, pixels[13]);
        Assert.Equal(255, pixels[14]);
    }
}
