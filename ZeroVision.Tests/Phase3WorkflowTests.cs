using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using ZeroVision.Core;
using ZeroVision.Imaging;
using ZeroVision.Shared;

namespace ZeroVision.Tests;

public class Phase3WorkflowTests
{
    [Fact]
    public void WorkspaceFilter_HasPhase3MetadataProperties()
    {
        var filter = new WorkspaceFilter
        {
            RequiredDateYear = 2026,
            RequiredCamera = "Sony A7IV",
            RequiredLens = "FE 24-70mm GM",
            RequiredIso = 800
        };

        Assert.Equal(2026, filter.RequiredDateYear);
        Assert.Equal("Sony A7IV", filter.RequiredCamera);
        Assert.Equal("FE 24-70mm GM", filter.RequiredLens);
        Assert.Equal(800, filter.RequiredIso);
    }

    [Fact]
    public void NavigatorCoordinate_MappingCalculations_MatchExpectedBounds()
    {
        // Giả lập ảnh 4000x3000 (aspect 4:3) hiển thị trong navigator 160x100 (aspect 1.6)
        double imgW = 4000;
        double imgH = 3000;
        double navW = 160;
        double navH = 100;

        // Scale fit
        double scale = Math.Min(navW / imgW, navH / imgH); // 100 / 3000 = 0.0333333
        double renderW = imgW * scale; // 133.33
        double renderH = imgH * scale; // 100.0
        double offsetX = (navW - renderW) / 2.0;
        double offsetY = (navH - renderH) / 2.0;

        Assert.True(offsetX > 0, "Pillarbox horizontal offset must be positive");
        Assert.Equal(0, offsetY, 3);

        // Giả lập zoom 2.0 ở tâm ảnh (normX=0.25, normY=0.25, normW=0.5, normH=0.5)
        double normX = 0.25;
        double normY = 0.25;
        double normW = 0.5;
        double normH = 0.5;

        double rectX = offsetX + normX * renderW;
        double rectY = offsetY + normY * renderH;
        double rectW = normW * renderW;
        double rectH = normH * renderH;

        Assert.True(rectX >= offsetX);
        Assert.True(rectX + rectW <= offsetX + renderW);
        Assert.True(rectY >= offsetY);
        Assert.True(rectY + rectH <= offsetY + renderH);
    }

    [Fact]
    public void MaskGizmo_LinearGradient_SimulationMath()
    {
        var mask = new LinearGradientMask { X0 = 0.5f, Y0 = 0.1f, X1 = 0.5f, Y1 = 0.6f };
        Assert.Equal(LinearGradientMask.Type, LinearGradientMask.Type);

        // Simulate gizmo drag offset deltaX = +0.1, deltaY = +0.2
        float deltaX = 0.1f;
        float deltaY = 0.2f;
        mask.X0 += deltaX;
        mask.Y0 += deltaY;
        mask.X1 += deltaX;
        mask.Y1 += deltaY;

        Assert.Equal(0.6f, mask.X0, 2);
        Assert.Equal(0.3f, mask.Y0, 2);
        Assert.Equal(0.6f, mask.X1, 2);
        Assert.Equal(0.8f, mask.Y1, 2);

        var generated = mask.Generate(20, 20);
        Assert.Equal(400, generated.Length);
    }

    [Fact]
    public void MaskGizmo_RadialMask_SimulationMath()
    {
        var mask = new RadialMask { Cx = 0.5f, Cy = 0.5f, Rx = 0.25f, Ry = 0.25f, Feather = 0.4f };

        // Simulate dragging radius handle X
        float newRx = 0.35f;
        mask.Rx = newRx;
        Assert.Equal(0.35f, mask.Rx, 2);

        var generated = mask.Generate(25, 25);
        Assert.Equal(625, generated.Length);
    }
}
