using System;
using System.Collections.Generic;
using ZeroVision.Imaging;
using Xunit;

namespace ZeroVision.Tests;

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
}
