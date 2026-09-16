using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroVision.Imaging;

/// <summary>
/// Contrast Limited Adaptive Histogram Equalization (CLAHE) Op.
/// Leverages ZeroGraphics.Imaging.Filters.ClaheFilter to enhance micro-contrast and recover
/// deep shadow and highlight details with zero halo artifacts.
/// </summary>
public sealed class ClaheOp : IEditOp
{
    public const string Type = "Clahe";
    public string OpType => Type;

    /// <summary>Effect intensity [0..1] (0 = identity / off).</summary>
    public float Amount = 0f;

    /// <summary>Contrast clip limit threshold (1.0 to 10.0, default 3.0).</summary>
    public float ClipLimit = 3.0f;

    /// <summary>Horizontal grid tile count (2..32, default 8).</summary>
    public int TilesX = 8;

    /// <summary>Vertical grid tile count (2..32, default 8).</summary>
    public int TilesY = 8;

    public bool IsIdentity => Amount < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;
        int w = image.Width;
        int h = image.Height;
        if (w < 8 || h < 8) return;

        // Allocate temporary ImageBuffer in Gray8 for luminance processing
        using var graySrc = new ImageBuffer(w, h, ImageFormatMode.Gray8);
        using var grayDst = new ImageBuffer(w, h, ImageFormatMode.Gray8);

        unsafe
        {
            // 1. Extract perceived luminance to Gray8 buffer
            for (int y = 0; y < h; y++)
            {
                byte* row = graySrc.GetRowPointer(y);
                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w + x) * 4;
                    float r = image.Pixels[idx];
                    float g = image.Pixels[idx + 1];
                    float b = image.Pixels[idx + 2];
                    float lum = ColorSpace.LumR * r + ColorSpace.LumG * g + ColorSpace.LumB * b;
                    row[x] = ColorSpace.EncodeByte(lum);
                }
            }

            // 2. Apply CLAHE from ZeroGraphics
            ClaheFilter.Apply(graySrc, grayDst, ClipLimit, TilesX, TilesY);

            // 3. Modulate original linear image by CLAHE luminance transfer ratio
            float amount = Math.Clamp(Amount, 0f, 1f);

            for (int y = 0; y < h; y++)
            {
                byte* sRow = graySrc.GetRowPointer(y);
                byte* dRow = grayDst.GetRowPointer(y);

                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w + x) * 4;
                    float inLum = ColorSpace.DecodeByte(sRow[x]);
                    float outLum = ColorSpace.DecodeByte(dRow[x]);

                    if (inLum > 1e-5f)
                    {
                        float ratio = outLum / inLum;
                        float effRatio = 1.0f + (ratio - 1.0f) * amount;
                        image.Pixels[idx] *= effRatio;
                        image.Pixels[idx + 1] *= effRatio;
                        image.Pixels[idx + 2] *= effRatio;
                    }
                    else if (outLum > inLum)
                    {
                        float boost = (outLum - inLum) * amount;
                        image.Pixels[idx] += boost;
                        image.Pixels[idx + 1] += boost;
                        image.Pixels[idx + 2] += boost;
                    }
                }
            }
        }
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["amount"] = Amount.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        ["clipLimit"] = ClipLimit.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        ["tilesX"] = TilesX.ToString(),
        ["tilesY"] = TilesY.ToString(),
    };

    public static ClaheOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        Amount = EditOpRegistry.F(p, "amount", 0f),
        ClipLimit = EditOpRegistry.F(p, "clipLimit", 3.0f),
        TilesX = EditOpRegistry.I(p, "tilesX", 8),
        TilesY = EditOpRegistry.I(p, "tilesY", 8),
    };

    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
