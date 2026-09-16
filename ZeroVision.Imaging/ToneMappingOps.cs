using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZeroVision.Imaging;

/// <summary>
/// Dehaze: xấp xỉ dark-channel prior. Ước lượng "độ mờ" cục bộ từ kênh tối (min RGB) đã blur,
/// rồi tăng tương phản + bão hoà ngược theo độ mờ. amount>0 khử mờ, &lt;0 thêm mờ (atmospheric).
/// </summary>
public sealed class DehazeOp : IEditOp
{
    public const string Type = "Dehaze";
    public string OpType => Type;
    public float Amount; // [-1..1]
    public float BaseRadius = 30f;

    public bool IsIdentity => MathF.Abs(Amount) < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;
        int w = image.Width, h = image.Height;
        int patchRadius = (int)MathF.Max(3f, MathF.Round(BaseRadius * scale * 0.2f));
        ZeroGraphics.Imaging.Filters.DehazeFilter.ApplyRgbaFloat(image.Pixels, w, h, Amount, 0.95f, patchRadius);
    }

    public Dictionary<string, string> ToParams() => new() { ["amount"] = F(Amount) };
    private static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public static DehazeOp FromParams(IReadOnlyDictionary<string, string> p)
        => new() { Amount = EditOpRegistry.F(p, "amount") };
    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}

/// <summary>
/// Filmic tone mapping (chuẩn Filmic / ACES curve): nén dải động lớn về [0..1]
/// bằng đường cong S filmic, giữ highlight không cháy. amount điều khiển mức nén.
/// </summary>
public sealed class FilmicOp : IEditOp
{
    public const string Type = "Filmic";
    public string OpType => Type;
    public float Amount; // [0..1] mức áp

    public bool IsIdentity => Amount < 1e-4f;

    // ACES filmic approx (Narkowicz).
    private static float Aces(float x)
    {
        const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;
        return Math.Clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0f, 1f);
    }

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;
        float amt = Math.Clamp(Amount, 0f, 1f);
        image.ProcessPixels((ref float r, ref float g, ref float b, ref float a) =>
        {
            float tr = Aces(r), tg = Aces(g), tb = Aces(b);
            r = r + (tr - r) * amt;
            g = g + (tg - g) * amt;
            b = b + (tb - b) * amt;
        });
    }

    public Dictionary<string, string> ToParams() => new() { ["amount"] = F(Amount) };
    private static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public static FilmicOp FromParams(IReadOnlyDictionary<string, string> p)
        => new() { Amount = EditOpRegistry.F(p, "amount") };
    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}

/// <summary>
/// Parametric Curve chuẩn 4 vùng (Highlights/Lights/Darks/Shadows) điều chỉnh bằng
/// trọng số mượt theo vị trí tông. Nội bộ dựng curve rồi áp như tone curve nhưng tham số hoá
/// theo 4 thanh trượt thay vì điểm.
/// </summary>
public sealed class ParametricCurveOp : IEditOp
{
    public const string Type = "ParametricCurve";
    public string OpType => Type;
    public float Highlights, Lights, Darks, Shadows; // [-1..1]

    public bool IsIdentity =>
        Near(Highlights) && Near(Lights) && Near(Darks) && Near(Shadows);
    private static bool Near(float v) => MathF.Abs(v) < 1e-4f;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;
        // dựng LUT 256 từ 4 vùng.
        var lut = new float[256];
        for (int i = 0; i < 256; i++)
        {
            float x = i / 255f;
            float wSh = Weight(x, 0.0f), wDk = Weight(x, 0.33f), wLt = Weight(x, 0.66f), wHi = Weight(x, 1.0f);
            float delta = (Shadows * wSh + Darks * wDk + Lights * wLt + Highlights * wHi) * 0.25f;
            lut[i] = Math.Clamp(x + delta, 0f, 1f);
        }

        image.ProcessPixels((ref float r, ref float g, ref float b, ref float a) =>
        {
            r = ColorSpace.SrgbToLinear(Sample(lut, ColorSpace.LinearToSrgb(r)));
            g = ColorSpace.SrgbToLinear(Sample(lut, ColorSpace.LinearToSrgb(g)));
            b = ColorSpace.SrgbToLinear(Sample(lut, ColorSpace.LinearToSrgb(b)));
        });
    }

    private static float Weight(float x, float center)
    {
        float d = MathF.Abs(x - center);
        const float wnd = 0.4f;
        if (d >= wnd) return 0f;
        return 0.5f * (1f + MathF.Cos(MathF.PI * d / wnd));
    }
    private static float Sample(float[] lut, float x)
    {
        if (x <= 0f) return lut[0];
        if (x >= 1f) return lut[255];
        float fx = x * 255f; int i = (int)fx; float fr = fx - i;
        return i >= 255 ? lut[255] : lut[i] + (lut[i + 1] - lut[i]) * fr;
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["hi"] = F(Highlights), ["lt"] = F(Lights), ["dk"] = F(Darks), ["sh"] = F(Shadows),
    };
    private static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public static ParametricCurveOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        Highlights = EditOpRegistry.F(p, "hi"), Lights = EditOpRegistry.F(p, "lt"),
        Darks = EditOpRegistry.F(p, "dk"), Shadows = EditOpRegistry.F(p, "sh"),
    };
    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
