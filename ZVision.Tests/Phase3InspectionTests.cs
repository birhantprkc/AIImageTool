using System;
using System.Collections.Generic;
using ZVision.Imaging;
using Xunit;

namespace ZVision.Tests;

public class Phase3InspectionTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 2)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(7, 3, 3)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 4, 3)]
    [InlineData(12, 4, 3)]
    [InlineData(16, 4, 4)]
    public void SurveyGrid_CalculatesBalancedColumnsAndRows(int count, int expectedCols, int expectedRows)
    {
        int cols, rows;
        if (count <= 1) { cols = 1; rows = 1; }
        else if (count == 2) { cols = 2; rows = 1; }
        else if (count <= 4) { cols = 2; rows = (count + 1) / 2; }
        else if (count <= 6) { cols = 3; rows = 2; }
        else if (count <= 9) { cols = 3; rows = 3; }
        else if (count <= 12) { cols = 4; rows = 3; }
        else { cols = 4; rows = (int)Math.Ceiling(count / 4.0); }

        Assert.Equal(expectedCols, cols);
        Assert.Equal(expectedRows, rows);
    }

    [Theory]
    [InlineData(0.0f, 0.0f, 1000, 800, 260, 130, 0, 0)]
    [InlineData(0.5f, 0.5f, 1000, 800, 260, 130, 370, 335)]
    [InlineData(1.0f, 1.0f, 1000, 800, 260, 130, 740, 670)]
    [InlineData(0.5f, 0.5f, 200, 100, 260, 130, 0, 0)] // Image smaller than loupe patch
    public void DetailLoupe_CropRectStaysWithinImageBounds(
        float normX, float normY,
        int imgW, int imgH,
        int patchW, int patchH,
        int expectedX, int expectedY)
    {
        int curW = Math.Min(patchW, imgW);
        int curH = Math.Min(patchH, imgH);

        int cx = (int)(normX * imgW);
        int cy = (int)(normY * imgH);

        int x = Math.Clamp(cx - curW / 2, 0, Math.Max(0, imgW - curW));
        int y = Math.Clamp(cy - curH / 2, 0, Math.Max(0, imgH - curH));

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.True(x >= 0 && x + curW <= imgW);
        Assert.True(y >= 0 && y + curH <= imgH);
    }

    [Fact]
    public void DiffuseOp_ExtendedParameters_RoundTripCorrectly()
    {
        var op = new DiffuseOp
        {
            Amount = 0.75f,
            Iterations = 10,
            EdgeSensitivity = 0.85f
        };

        var dict = op.ToParams();
        var reconstructed = DiffuseOp.FromParams(dict);

        Assert.Equal(op.Amount, reconstructed.Amount, 3);
        Assert.Equal(op.Iterations, reconstructed.Iterations);
        Assert.Equal(op.EdgeSensitivity, reconstructed.EdgeSensitivity, 3);
    }

    [Fact]
    public void GlowOp_ExtendedParameters_RoundTripCorrectly()
    {
        var op = new GlowOp
        {
            Amount = 0.45f,
            BaseRadius = 24f,
            Threshold = 0.65f
        };

        var dict = op.ToParams();
        var reconstructed = GlowOp.FromParams(dict);

        Assert.Equal(op.Amount, reconstructed.Amount, 3);
        Assert.Equal(op.BaseRadius, reconstructed.BaseRadius, 3);
        Assert.Equal(op.Threshold, reconstructed.Threshold, 3);
    }

    [Theory]
    [InlineData(0, 1, 4, 1)]     // Move right
    [InlineData(3, 1, 4, 0)]     // Wrap around right
    [InlineData(0, -1, 4, 3)]    // Wrap around left
    [InlineData(2, 2, 4, 0)]     // Move down row in 2-col grid
    [InlineData(1, -2, 4, 3)]    // Move up row in 2-col grid with wrap
    public void SurveyNavigation_CyclicIndex_WrapsAroundCorrectly(int currentIdx, int delta, int count, int expectedIdx)
    {
        int next = (currentIdx + delta % count + count) % count;
        Assert.Equal(expectedIdx, next);
    }

    [Theory]
    [InlineData(1.0, 1.25, 1.25)]
    [InlineData(1.25, 5.0, 5.0)]   // Max clamp
    [InlineData(1.0, 0.5, 1.0)]    // Min clamp
    [InlineData(1.03, 0.98, 1.0)]  // Reset to 1.0 when <= 1.02
    public void SurveyZoom_ClampingAndReset_BehavesPredictably(double startZoom, double factor, double expectedZoom)
    {
        double zoom = Math.Clamp(startZoom * factor, 1.0, 5.0);
        if (zoom <= 1.02)
        {
            zoom = 1.0;
        }
        Assert.Equal(expectedZoom, zoom, 2);
    }
}
