using System.Collections.Generic;
using ZVision.Imaging;
using Xunit;

namespace ZVision.Tests;

public class NewFeatureOpsTests
{
    private static LinearImage Solid(float r, float g, float b, int w = 8, int h = 8)
    {
        var img = new LinearImage(w, h);
        for (int i = 0; i < img.Pixels.Length; i += 4)
        { img.Pixels[i] = r; img.Pixels[i + 1] = g; img.Pixels[i + 2] = b; img.Pixels[i + 3] = 1f; }
        return img;
    }

    // ---- BlackWhiteOp (13.1) ----

    [Fact]
    public void BlackWhite_Disabled_IsIdentity()
    {
        var op = new BlackWhiteOp { Enabled = false };
        Assert.True(op.IsIdentity);
        var img = Solid(0.6f, 0.3f, 0.1f);
        op.Apply(img, 1f);
        Assert.InRange(img.Pixels[0], 0.599f, 0.601f);
    }

    [Fact]
    public void BlackWhite_ProducesGray()
    {
        var op = new BlackWhiteOp { Enabled = true };
        var img = Solid(0.6f, 0.3f, 0.1f);
        op.Apply(img, 1f);
        // R=G=B sau khi chuyển xám.
        Assert.Equal(img.Pixels[0], img.Pixels[1], 4);
        Assert.Equal(img.Pixels[1], img.Pixels[2], 4);
    }

    [Fact]
    public void BlackWhite_ChannelWeights_AffectGray()
    {
        var imgRed = Solid(0.8f, 0.1f, 0.1f);
        var imgRedHeavy = Solid(0.8f, 0.1f, 0.1f);
        new BlackWhiteOp { Enabled = true, RedWeight = 0.1f, GreenWeight = 0.8f, BlueWeight = 0.1f }.Apply(imgRed, 1f);
        new BlackWhiteOp { Enabled = true, RedWeight = 0.8f, GreenWeight = 0.1f, BlueWeight = 0.1f }.Apply(imgRedHeavy, 1f);
        // ảnh đỏ: tăng trọng số đỏ -> xám sáng hơn.
        Assert.True(imgRedHeavy.Pixels[0] > imgRed.Pixels[0]);
    }

    [Fact]
    public void BlackWhite_RoundTrip()
    {
        var op = new BlackWhiteOp { Enabled = true, RedWeight = 0.4f, ToneHue = 40f, ToneStrength = 0.3f };
        var back = BlackWhiteOp.FromParams(op.ToParams());
        Assert.True(back.Enabled);
        Assert.Equal(0.4f, back.RedWeight, 4);
        Assert.Equal(0.3f, back.ToneStrength, 4);
    }

    [Fact]
    public void BlackWhite_RedFilter_DarkensBlueSky_VsBlueFilter()
    {
        // Trời xanh: red filter (kính lọc đỏ) làm trời TỐI hơn; blue filter làm trời SÁNG hơn.
        var sky = Solid(0.2f, 0.4f, 0.8f);
        var skyRed = Solid(0.2f, 0.4f, 0.8f);
        var skyBlue = Solid(0.2f, 0.4f, 0.8f);
        // weights như preset trong UI.
        new BlackWhiteOp { Enabled = true, RedWeight = 0.80f, GreenWeight = 0.15f, BlueWeight = 0.05f }.Apply(skyRed, 1f);
        new BlackWhiteOp { Enabled = true, RedWeight = 0.05f, GreenWeight = 0.25f, BlueWeight = 0.70f }.Apply(skyBlue, 1f);
        Assert.True(skyRed.Pixels[0] < skyBlue.Pixels[0], "red filter phải làm trời xanh tối hơn blue filter");
    }

    // ---- InvertOp (13.3) ----

    [Fact]
    public void Invert_Disabled_IsIdentity()
    {
        var op = new InvertOp { Enabled = false };
        Assert.True(op.IsIdentity);
    }

    [Fact]
    public void Invert_BlackBecomesWhite()
    {
        var img = Solid(0f, 0f, 0f);
        new InvertOp { Enabled = true }.Apply(img, 1f);
        // đen -> trắng (linear ~1).
        Assert.True(img.Pixels[0] > 0.99f);
    }

    [Fact]
    public void Invert_Twice_RestoresOriginal()
    {
        var img = Solid(ColorSpace.SrgbToLinear(0.3f), ColorSpace.SrgbToLinear(0.6f), ColorSpace.SrgbToLinear(0.8f));
        float r0 = img.Pixels[0], g0 = img.Pixels[1], b0 = img.Pixels[2];
        var op = new InvertOp { Enabled = true };
        op.Apply(img, 1f);
        op.Apply(img, 1f);
        Assert.InRange(img.Pixels[0], r0 - 1e-3f, r0 + 1e-3f);
        Assert.InRange(img.Pixels[1], g0 - 1e-3f, g0 + 1e-3f);
        Assert.InRange(img.Pixels[2], b0 - 1e-3f, b0 + 1e-3f);
    }

    // ---- ChannelGainOp (13.2 apply) ----

    [Fact]
    public void ChannelGain_Identity_NoChange()
    {
        var op = new ChannelGainOp();
        Assert.True(op.IsIdentity);
        var img = Solid(0.5f, 0.5f, 0.5f);
        op.Apply(img, 1f);
        Assert.InRange(img.Pixels[0], 0.499f, 0.501f);
    }

    [Fact]
    public void ChannelGain_ScalesChannels()
    {
        var img = Solid(0.4f, 0.4f, 0.4f);
        new ChannelGainOp { R = 1.5f, G = 1f, B = 0.5f }.Apply(img, 1f);
        Assert.InRange(img.Pixels[0], 0.599f, 0.601f);
        Assert.InRange(img.Pixels[2], 0.199f, 0.201f);
    }

    // ---- AutoWhiteBalance (13.2) ----

    [Fact]
    public void AutoWB_GrayWorld_NeutralizesCast()
    {
        // ảnh ám đỏ: R cao hơn -> gain R < 1, gain B > 1.
        var img = Solid(0.6f, 0.4f, 0.3f);
        var gains = AutoWhiteBalance.Analyze(img, AutoWhiteBalance.Strategy.GrayWorld);
        Assert.True(gains.R < 1f);
        Assert.True(gains.B > 1f);
        Assert.Equal(1f, gains.G, 4); // chuẩn hoá theo G
    }

    [Fact]
    public void AutoWB_NeutralImage_NoCorrection()
    {
        var img = Solid(0.5f, 0.5f, 0.5f);
        var gains = AutoWhiteBalance.Analyze(img);
        Assert.True(gains.IsNeutral);
    }

    [Fact]
    public void AutoWB_AppliedGains_BalanceChannels()
    {
        var img = Solid(0.6f, 0.4f, 0.3f);
        var gains = AutoWhiteBalance.Analyze(img);
        new ChannelGainOp { R = gains.R, G = gains.G, B = gains.B }.Apply(img, 1f);
        // sau cân bằng, 3 kênh xích lại gần nhau hơn.
        float spread = System.MathF.Max(img.Pixels[0], System.MathF.Max(img.Pixels[1], img.Pixels[2]))
                     - System.MathF.Min(img.Pixels[0], System.MathF.Min(img.Pixels[1], img.Pixels[2]));
        Assert.True(spread < 0.3f - 0.05f); // ban đầu spread = 0.3
    }

    [Fact]
    public void AutoWB_WhitePatch_Works()
    {
        var img = Solid(0.8f, 0.6f, 0.4f);
        var gains = AutoWhiteBalance.Analyze(img, AutoWhiteBalance.Strategy.WhitePatch);
        Assert.True(gains.B > gains.R); // bù kênh yếu nhất (B) nhiều hơn
    }

    // ---- AutoWhiteBalance GrayEdge ----

    [Fact]
    public void AutoWB_GrayEdge_NeutralizesCastOnEdges()
    {
        var img = new LinearImage(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int p = (y * 32 + x) * 4;
                float factor = (x > 16) ? 1.5f : 1.0f;
                img.Pixels[p] = 0.5f * factor;
                img.Pixels[p + 1] = 0.35f * factor;
                img.Pixels[p + 2] = 0.25f * factor;
                img.Pixels[p + 3] = 1.0f;
            }
        }

        var gains = AutoWhiteBalance.Analyze(img, AutoWhiteBalance.Strategy.GrayEdge);
        Assert.True(gains.R < 1.0f, $"Expected gains.R < 1, got {gains.R}");
        Assert.True(gains.B > 1.0f, $"Expected gains.B > 1, got {gains.B}");
        Assert.Equal(1f, gains.G, 3);
    }

    // ---- GuidedFilterOp ----

    [Fact]
    public void GuidedFilterOp_SmoothesTexturePreservingEdges()
    {
        int w = 32, h = 32;
        var img = new LinearImage(w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                // Sharp step edge at x = 16: left = 0.2, right = 0.8
                float baseVal = (x < 16) ? 0.2f : 0.8f;
                // High frequency noise texture: alternating +/- 0.05
                float noise = ((x + y) % 2 == 0) ? 0.05f : -0.05f;
                float val = baseVal + noise;
                img.Pixels[p] = val;
                img.Pixels[p + 1] = val;
                img.Pixels[p + 2] = val;
                img.Pixels[p + 3] = 1.0f;
            }
        }

        var op = new GuidedFilterOp { BaseRadius = 4f, Eps = 0.01f, Subsample = 1, Amount = 1.0f };
        op.Apply(img, 1.0f);

        // Texture noise in flat area should be smoothed (from original 0.1 peak-to-peak down to < 0.03 variation)
        float v1 = img.Pixels[(8 * w + 5) * 4];
        float v2 = img.Pixels[(8 * w + 6) * 4];
        Assert.True(MathF.Abs(v1 - v2) < 0.03f, $"Expected texture smoothed, diff={MathF.Abs(v1 - v2)}");

        // Step edge at boundary (x=13 vs x=18) should remain sharp (> 0.5 step)
        float left = img.Pixels[(16 * w + 13) * 4];
        float right = img.Pixels[(16 * w + 18) * 4];
        Assert.True(right - left > 0.5f, $"Expected sharp edge preserved, diff={right - left}");
    }

    [Fact]
    public void GuidedFilterOp_SubsampledRunsSuccessfully()
    {
        int w = 64, h = 64;
        var img = new LinearImage(w, h);
        var op = new GuidedFilterOp { BaseRadius = 8f, Eps = 0.02f, Subsample = 2, Amount = 1.0f };
        op.Apply(img, 1.0f);
        Assert.NotNull(img);
    }

    // ---- DirectedMedianOp ----

    [Fact]
    public void DirectedMedianOp_RemovesSpikePreservingLine()
    {
        int w = 16, h = 16;
        var img = new LinearImage(w, h);
        // Vertical 1-pixel thin line at x = 8: value = 0.8f, background = 0.1f
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                float v = (x == 8) ? 0.8f : 0.1f;
                img.Pixels[p] = v;
                img.Pixels[p + 1] = v;
                img.Pixels[p + 2] = v;
                img.Pixels[p + 3] = 1.0f;
            }
        }

        // Add an isolated hot spike at (4, 4)
        int spikeP = (4 * w + 4) * 4;
        img.Pixels[spikeP] = 1.0f;
        img.Pixels[spikeP + 1] = 1.0f;
        img.Pixels[spikeP + 2] = 1.0f;

        var op = new DirectedMedianOp { Threshold = 0.05f, Strength = 1.0f };
        op.Apply(img, 1.0f);

        // Hot spike should be suppressed to background (~0.1f)
        Assert.True(img.Pixels[spikeP] < 0.25f, $"Spike not suppressed: {img.Pixels[spikeP]}");

        // Thin line at (8, 8) must NOT be erased by median
        int lineP = (8 * w + 8) * 4;
        Assert.True(img.Pixels[lineP] > 0.7f, $"Thin line erased: {img.Pixels[lineP]}");
    }

    // ---- All registered ----

    [Fact]
    public void NewOps_Registered()
    {
        var reg = EditOpRegistry.CreateDefault();
        Assert.True(reg.Has(BlackWhiteOp.Type));
        Assert.True(reg.Has(InvertOp.Type));
        Assert.True(reg.Has(ChannelGainOp.Type));
        Assert.True(reg.Has(GuidedFilterOp.Type));
        Assert.True(reg.Has(DirectedMedianOp.Type));
    }
}

public class CropAspectTests
{
    [Fact]
    public void Square_FromLandscape_LimitedByHeight()
    {
        var r = CropAspect.Centered(200, 100, 1, 1);
        // ảnh 200x100, crop vuông -> 100x100, w=0.5, căn giữa x=0.25.
        Assert.Equal(0.5f, r.W, 3);
        Assert.Equal(1f, r.H, 3);
        Assert.Equal(0.25f, r.X, 3);
        Assert.Equal(0f, r.Y, 3);
    }

    [Fact]
    public void Square_FromPortrait_LimitedByWidth()
    {
        var r = CropAspect.Centered(100, 200, 1, 1);
        Assert.Equal(1f, r.W, 3);
        Assert.Equal(0.5f, r.H, 3);
        Assert.Equal(0f, r.X, 3);
        Assert.Equal(0.25f, r.Y, 3);
    }

    [Fact]
    public void SixteenNine_FromSquare()
    {
        var r = CropAspect.Centered(100, 100, 16, 9);
        // 16:9 trong ảnh vuông -> full width, height = 100*9/16 = 56.25px -> h ~0.5625.
        Assert.Equal(1f, r.W, 3);
        Assert.Equal(0.5625f, r.H, 3);
    }

    [Fact]
    public void MatchingAspect_FullFrame()
    {
        var r = CropAspect.Centered(160, 90, 16, 9);
        Assert.Equal(1f, r.W, 3);
        Assert.Equal(1f, r.H, 3);
    }

    [Fact]
    public void InvalidRatio_ReturnsFull()
    {
        var r = CropAspect.Centered(100, 100, 0, 0);
        Assert.Equal(1f, r.W, 3);
        Assert.Equal(1f, r.H, 3);
    }

    [Fact]
    public void Presets_ContainsCommon()
    {
        Assert.Contains(CropAspect.Presets, p => p.Name == "16:9");
        Assert.Contains(CropAspect.Presets, p => p.Name == "1:1");
    }
}
