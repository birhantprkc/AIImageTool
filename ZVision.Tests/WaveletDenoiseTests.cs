using System;
using System.Collections.Generic;
using Xunit;
using ZVision.Imaging;

namespace ZVision.Tests;

public class WaveletDenoiseTests
{
    [Fact]
    public void WaveletDenoiseOp_AttenuatesNoisePreservingEdges()
    {
        int w = 32, h = 32;
        var img = new LinearImage(w, h);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                float baseVal = (x < 16) ? 0.2f : 0.8f;
                float noise = ((x + y) % 2 == 0) ? 0.05f : -0.05f;
                float v = baseVal + noise;

                img.Pixels[p] = v;
                img.Pixels[p + 1] = v;
                img.Pixels[p + 2] = v;
                img.Pixels[p + 3] = 1.0f;
            }
        }

        var op = new WaveletDenoiseOp { LumaAmount = 0.8f, ChromaAmount = 0.5f, Scales = 3 };
        op.Apply(img, 1.0f);

        // Check flat noise is attenuated
        float v1 = img.Pixels[(8 * w + 5) * 4];
        float v2 = img.Pixels[(8 * w + 6) * 4];
        float diff = MathF.Abs(v1 - v2);
        Assert.True(diff < 0.04f, $"Noise not sufficiently suppressed: diff={diff}");

        // Check step edge height between x=13 and x=18 is preserved
        float left = img.Pixels[(16 * w + 13) * 4];
        float right = img.Pixels[(16 * w + 18) * 4];
        Assert.True(right - left > 0.5f, $"Step edge height compromised: {right - left}");
    }

    [Fact]
    public void WaveletDenoiseOp_RegisteredInRegistry()
    {
        var reg = EditOpRegistry.CreateDefault();
        Assert.True(reg.Has(WaveletDenoiseOp.Type));

        var op = reg.Create(WaveletDenoiseOp.Type, new Dictionary<string, string>
        {
            ["luma"] = "0.75",
            ["chroma"] = "0.6",
            ["scales"] = "3"
        }) as WaveletDenoiseOp;

        Assert.NotNull(op);
        Assert.Equal(0.75f, op.LumaAmount, 2);
        Assert.Equal(0.6f, op.ChromaAmount, 2);
        Assert.Equal(3, op.Scales);
    }

    [Fact]
    public void AiInpaintOp_TeleaFastMarchingRestoresBlemish()
    {
        int w = 32, h = 32;
        var img = new LinearImage(w, h);

        // Uniform background 0.7
        for (int i = 0; i < img.Pixels.Length; i += 4)
        {
            img.Pixels[i] = 0.7f;
            img.Pixels[i + 1] = 0.7f;
            img.Pixels[i + 2] = 0.7f;
            img.Pixels[i + 3] = 1.0f;
        }

        // Add a black blemish/dust spot at (16, 16)
        int centerP = (16 * w + 16) * 4;
        img.Pixels[centerP] = 0.0f;
        img.Pixels[centerP + 1] = 0.0f;
        img.Pixels[centerP + 2] = 0.0f;

        var op = new AiInpaintOp
        {
            Algorithm = InpaintAlgorithm.FastTeleaDiffusion,
            Strength = 1.0f
        };
        // Normalized coordinate (0.5, 0.5), radius ~0.1
        op.Regions.Add(new AiInpaintOp.InpaintRegion(0.5f, 0.5f, 0.1f));
        op.Apply(img, 1.0f);

        // Center pixel should be restored to ambient 0.7f
        float restored = img.Pixels[centerP];
        Assert.True(restored > 0.6f, $"Blemish not restored: {restored}");
    }
}
