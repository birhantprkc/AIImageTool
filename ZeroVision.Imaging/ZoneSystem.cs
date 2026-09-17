using System;
using ZeroGraphics.Imaging.Photometry;

namespace ZeroVision.Imaging;

/// <summary>
/// Ansel Adams Zone System bridge delegating to ZeroGraphics.Imaging.Photometry.ZoneSystemMeter.
/// </summary>
public static class ZoneSystem
{
    public const float MiddleGrayLinear = ZoneSystemMeter.MiddleGrayLinear;
    public static readonly (byte B, byte G, byte R, byte A)[] ZoneColors = Array.ConvertAll(ZoneSystemMeter.ZoneColors, c => (c.B, c.G, c.R, c.A));
    public static readonly (byte B, byte G, byte R, byte A)[] LuminanceZoneLut = Array.ConvertAll(ZoneSystemMeter.LuminanceZoneLut, c => (c.B, c.G, c.R, c.A));

    public static int GetZoneIndex(float linearY)
    {
        byte sY = ColorSpace.EncodeByte(linearY);
        return ZoneSystemMeter.GetZoneFromSrgbByte(sY);
    }

    public static int GetZoneFromSrgbByte(byte sY) => ZoneSystemMeter.GetZoneFromSrgbByte(sY);

    public static void MapBgraPixelsToZoneMask(Span<byte> bgraPixels)
        => ZoneSystemMeter.MapBgraPixelsToZoneMask(bgraPixels);
}
