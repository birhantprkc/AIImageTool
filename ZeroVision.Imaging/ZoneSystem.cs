using System;
using ZeroGraphics.Imaging.Filters;

namespace ZeroVision.Imaging;

/// <summary>
/// Ansel Adams Zone System bridge delegating to ZeroGraphics.Imaging.Filters.ZoneSystemMeter.
/// </summary>
public static class ZoneSystem
{
    public const float MiddleGrayLinear = ZoneSystemMeter.MiddleGrayLinear;
    public static readonly (byte B, byte G, byte R, byte A)[] ZoneColors = ZoneSystemMeter.ZoneColors;
    public static readonly (byte B, byte G, byte R, byte A)[] LuminanceZoneLut = ZoneSystemMeter.LuminanceZoneLut;

    public static int GetZoneIndex(float linearY)
    {
        byte sY = ColorSpace.EncodeByte(linearY);
        return ZoneSystemMeter.GetZoneFromSrgbByte(sY);
    }

    public static int GetZoneFromSrgbByte(byte sY) => ZoneSystemMeter.GetZoneFromSrgbByte(sY);

    public static void MapBgraPixelsToZoneMask(Span<byte> bgraPixels)
        => ZoneSystemMeter.MapBgraPixelsToZoneMask(bgraPixels);
}
