using System;
using System.Collections.Generic;
using Xunit;
using ZeroVision.Imaging;

namespace ZeroVision.Tests;

public class ExposureFusionServiceTests
{
    [Fact]
    public void SingleImage_ReturnsClone()
    {
        var img = new LinearImage(16, 16);
        var fused = ExposureFusionService.FuseExposures(new[] { img });
        Assert.Equal(16, fused.Width);
        Assert.NotSame(img, fused);
    }

    [Fact]
    public void MismatchedDimensions_Throws()
    {
        var img1 = new LinearImage(16, 16);
        var img2 = new LinearImage(32, 32);
        Assert.Throws<ArgumentException>(() => ExposureFusionService.FuseExposures(new[] { img1, img2 }));
        Assert.Throws<ArgumentException>(() => ExposureFusionService.FuseFocusStack(new[] { img1, img2 }));
    }

    [Fact]
    public void FuseExposures_RecoversHighlightsAndShadows()
    {
        int w = 32, h = 32;
        var under = new LinearImage(w, h);
        var over = new LinearImage(w, h);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                bool isShadow = x < 16;
                float u = isShadow ? 0.02f : 0.45f;
                float o = isShadow ? 0.50f : 0.98f;

                under.Pixels[p] = under.Pixels[p + 1] = under.Pixels[p + 2] = u; under.Pixels[p + 3] = 1.0f;
                over.Pixels[p] = over.Pixels[p + 1] = over.Pixels[p + 2] = o; over.Pixels[p + 3] = 1.0f;
            }
        }

        var fused = ExposureFusionService.FuseExposures(new[] { under, over }, maxLevels: 3);
        Assert.NotNull(fused);

        // Shadows recovered from 'over' (> 0.35)
        float shadowVal = fused.Pixels[(16 * w + 8) * 4];
        Assert.True(shadowVal > 0.35f, $"Shadow not recovered: {shadowVal}");

        // Highlights preserved from 'under' (< 0.65)
        float highlightVal = fused.Pixels[(16 * w + 24) * 4];
        Assert.True(highlightVal < 0.65f, $"Highlight not preserved: {highlightVal}");
    }

    [Fact]
    public void FuseFocusStack_CombinesSharpDetails()
    {
        int w = 32, h = 32;
        var leftSharp = new LinearImage(w, h);
        var rightSharp = new LinearImage(w, h);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                float pattern = ((x + y) % 2 == 0) ? 0.9f : 0.1f;
                float flat = 0.5f;

                float v1 = (x < 16) ? pattern : flat;
                float v2 = (x < 16) ? flat : pattern;

                leftSharp.Pixels[p] = leftSharp.Pixels[p + 1] = leftSharp.Pixels[p + 2] = v1; leftSharp.Pixels[p + 3] = 1.0f;
                rightSharp.Pixels[p] = rightSharp.Pixels[p + 1] = rightSharp.Pixels[p + 2] = v2; rightSharp.Pixels[p + 3] = 1.0f;
            }
        }

        var fused = ExposureFusionService.FuseFocusStack(new[] { leftSharp, rightSharp }, maxLevels: 3);
        Assert.NotNull(fused);

        // Left half should have preserved checkerboard sharpness
        float leftDiff = MathF.Abs(fused.Pixels[(4 * w + 4) * 4] - fused.Pixels[(4 * w + 5) * 4]);
        Assert.True(leftDiff > 0.3f, $"Left sharp zone not preserved: {leftDiff}");

        // Right half should have preserved checkerboard sharpness
        float rightDiff = MathF.Abs(fused.Pixels[(4 * w + 20) * 4] - fused.Pixels[(4 * w + 21) * 4]);
        Assert.True(rightDiff > 0.3f, $"Right sharp zone not preserved: {rightDiff}");
    }
}
