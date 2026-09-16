using System;

namespace ZeroVision.Imaging;

/// <summary>
/// Ansel Adams 11-Zone System Exposure Evaluation and Calibration Engine.
/// Standardizes photographic zones 0 to X relative to 18% Middle Gray (Zone V, 0.0 EV).
/// </summary>
public static class ZoneSystem
{
    public const float MiddleGrayLinear = 0.18f;

    /// <summary>
    /// Calibrated false colors for 11 Zones (B, G, R, A).
    /// </summary>
    public static readonly (byte B, byte G, byte R, byte A)[] ZoneColors =
    {
        (40, 0, 16, 225),       // Zone 0:  <= -4.5 EV (Pitch Navy / Pure Black)
        (126, 35, 26, 225),     // Zone I:  -4.0 EV (Deep Indigo)
        (161, 71, 13, 225),     // Zone II: -3.0 EV (Royal Blue)
        (143, 131, 0, 225),     // Zone III:-2.0 EV (Teal / Textured Shadow)
        (50, 125, 46, 225),     // Zone IV: -1.0 EV (Forest Green)
        (158, 158, 158, 225),   // Zone V:   0.0 EV (Neutral 18% Middle Gray)
        (53, 216, 253, 225),    // Zone VI: +1.0 EV (Warm Yellow)
        (0, 140, 251, 225),     // Zone VII:+2.0 EV (Amber Orange)
        (53, 57, 229, 225),     // Zone VIII:+3.0 EV (Coral Red)
        (96, 27, 216, 225),     // Zone IX: +4.0 EV (Magenta Pink)
        (255, 255, 255, 255)    // Zone X:  >= +4.5 EV (Specular Clip White)
    };

    public static readonly (byte B, byte G, byte R, byte A)[] LuminanceZoneLut = BuildLuminanceZoneLut();

    private static (byte B, byte G, byte R, byte A)[] BuildLuminanceZoneLut()
    {
        var lut = new (byte B, byte G, byte R, byte A)[256];
        for (int i = 0; i < 256; i++)
        {
            int zone = GetZoneFromSrgbByte((byte)i);
            lut[i] = ZoneColors[zone];
        }
        return lut;
    }

    /// <summary>
    /// Calculates the Ansel Adams Zone index (0 to 10) for a given linear luminance [0..1].
    /// </summary>
    public static int GetZoneIndex(float linearY)
    {
        byte sY = ColorSpace.EncodeByte(linearY);
        return GetZoneFromSrgbByte(sY);
    }

    /// <summary>
    /// Classifies an 8-bit perceptual sRGB luminance value (0..255) into an Ansel Adams Zone index (0 to 10).
    /// Calibrated to map:
    /// - Zone 0 (Extreme Black): 0..5
    /// - Zone I (Near Black): 6..20
    /// - Zone II (First Shadow Texture): 21..45
    /// - Zone III (Textured Shadow): 46..75
    /// - Zone IV (Dark Tones): 76..105
    /// - Zone V (18% Middle Gray ~118): 106..135
    /// - Zone VI (Light Skin / Tones): 136..165
    /// - Zone VII (Light Texture): 166..195
    /// - Zone VIII (Highlight Detail): 196..225
    /// - Zone IX (Glare / Near Clip): 226..250
    /// - Zone X (Specular Clip): 251..255
    /// </summary>
    public static int GetZoneFromSrgbByte(byte sY)
    {
        if (sY <= 5) return 0;
        if (sY <= 20) return 1;
        if (sY <= 45) return 2;
        if (sY <= 75) return 3;
        if (sY <= 105) return 4;
        if (sY <= 135) return 5;
        if (sY <= 165) return 6;
        if (sY <= 195) return 7;
        if (sY <= 225) return 8;
        if (sY <= 250) return 9;
        return 10;
    }

    /// <summary>
    /// Maps a raw BGRA pixel buffer in-place to false-color Zone System colors.
    /// </summary>
    public static void MapBgraPixelsToZoneMask(Span<byte> bgraPixels)
    {
        var lut = LuminanceZoneLut;
        for (int i = 0; i < bgraPixels.Length; i += 4)
        {
            byte b = bgraPixels[i];
            byte g = bgraPixels[i + 1];
            byte r = bgraPixels[i + 2];

            int lum = (54 * r + 183 * g + 19 * b) >> 8;
            if (lum > 255) lum = 255;

            var zColor = lut[lum];
            bgraPixels[i] = zColor.B;
            bgraPixels[i + 1] = zColor.G;
            bgraPixels[i + 2] = zColor.R;
            bgraPixels[i + 3] = zColor.A;
        }
    }
}
