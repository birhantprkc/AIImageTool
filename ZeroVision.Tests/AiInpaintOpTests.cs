using System;
using Xunit;
using ZeroVision.Imaging;

namespace ZeroVision.Tests;

public class AiInpaintOpTests
{
    [Fact]
    public void AiInpaintOp_IsRegisteredInDefaultRegistry()
    {
        var reg = EditOpRegistry.CreateDefault();
        Assert.True(reg.Has(AiInpaintOp.Type));
    }

    [Fact]
    public void AiInpaintOp_RoundTrip_Parameters()
    {
        var op = new AiInpaintOp
        {
            Strength = 0.85f,
            Iterations = 12
        };
        op.Regions.Add(new AiInpaintOp.InpaintRegion(0.25f, 0.5f, 0.08f));
        op.Regions.Add(new AiInpaintOp.InpaintRegion(0.75f, 0.8f, 0.04f));

        var p = op.ToParams();
        var reconstructed = (AiInpaintOp)AiInpaintOp.Create(p);

        Assert.Equal(0.85f, reconstructed.Strength, 2);
        Assert.Equal(12, reconstructed.Iterations);
        Assert.Equal(2, reconstructed.Regions.Count);
        Assert.Equal(0.25f, reconstructed.Regions[0].NormalizedX, 2);
    }

    [Fact]
    public void AiInpaintOp_DiffusesHoleTowardsAmbientColor()
    {
        int w = 32, h = 32;
        var img = new LinearImage(w, h);
        // Fill whole image with 1.0 (white)
        for (int i = 0; i < img.Pixels.Length; i += 4)
        {
            img.Pixels[i] = 1.0f;
            img.Pixels[i + 1] = 1.0f;
            img.Pixels[i + 2] = 1.0f;
            img.Pixels[i + 3] = 1.0f;
        }

        // Create a black spot at center (16, 16)
        int centerOffset = img.Offset(16, 16);
        img.Pixels[centerOffset] = 0.0f;
        img.Pixels[centerOffset + 1] = 0.0f;
        img.Pixels[centerOffset + 2] = 0.0f;

        var op = new AiInpaintOp
        {
            Strength = 1.0f,
            Iterations = 16
        };
        op.Regions.Add(new AiInpaintOp.InpaintRegion(0.5f, 0.5f, 0.1f));

        op.Apply(img, scale: 1.0f);

        // Center pixel should be diffused closer to surrounding white (1.0)
        float redAtCenter = img.Pixels[centerOffset];
        Assert.True(redAtCenter > 0.5f, $"Center pixel expected to be inpainted > 0.5, but was {redAtCenter}");
    }
}
