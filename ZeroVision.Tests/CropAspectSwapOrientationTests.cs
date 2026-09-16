using System;
using Xunit;
using ZeroVision.Imaging;

namespace ZeroVision.Tests;

public class CropAspectSwapOrientationTests
{
    [Fact]
    public void SwapOrientation_LandscapeToPortrait_ChangesAspect()
    {
        int imgW = 6000;
        int imgH = 4000;

        // Crop ngang: 3000px x 2000px (W=0.5, H=0.5) căn giữa
        float cropX = 0.25f, cropY = 0.25f, cropW = 0.5f, cropH = 0.5f;

        var swapped = CropAspect.SwapOrientation(imgW, imgH, cropX, cropY, cropW, cropH);

        // Sau khi đảo: pixel width = 2000px (2000/6000 = 0.3333), pixel height = 3000px (3000/4000 = 0.75)
        Assert.InRange(swapped.W, 0.32f, 0.34f);
        Assert.InRange(swapped.H, 0.74f, 0.76f);
        Assert.True(swapped.X >= 0f && swapped.X + swapped.W <= 1.0001f);
        Assert.True(swapped.Y >= 0f && swapped.Y + swapped.H <= 1.0001f);
    }

    [Fact]
    public void SwapOrientation_ExceedingBounds_ScalesDownToFit()
    {
        int imgW = 6000;
        int imgH = 4000;

        // Khung crop chiếm trọn chiều rộng (W=1.0, H=0.5 -> 6000px x 2000px)
        // Khi đảo: chiều cao mới là 6000px, nhưng ảnh chỉ cao 4000px -> phải scale xuống
        var swapped = CropAspect.SwapOrientation(imgW, imgH, 0f, 0.25f, 1.0f, 0.5f);

        Assert.True(swapped.W <= 1.0001f);
        Assert.True(swapped.H <= 1.0001f);
        Assert.True(swapped.X >= 0f && swapped.X + swapped.W <= 1.0001f);
        Assert.True(swapped.Y >= 0f && swapped.Y + swapped.H <= 1.0001f);
    }

    [Fact]
    public void SwapOrientation_ZeroDimensions_FallsBackToDirectSwap()
    {
        var swapped = CropAspect.SwapOrientation(0, 0, 0.2f, 0.1f, 0.6f, 0.4f);

        Assert.Equal(0.4f, swapped.W);
        Assert.Equal(0.6f, swapped.H);
        Assert.InRange(swapped.X, 0f, 0.6f);
        Assert.InRange(swapped.Y, 0f, 0.4f);
    }
}
