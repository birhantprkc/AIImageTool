using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ZeroVision.Imaging;

public enum LutInterpolation
{
    Tetrahedral = 0,
    Trilinear = 1
}

/// <summary>
/// Applies a 3D LUT (.cube) non-destructively within the pipeline. LUT operates in sRGB space
/// (standard for creative color grades and film profiles). Supports high-fidelity Tetrahedral
/// and Trilinear 3D lattice interpolation.
/// </summary>
public sealed class LutCubeOp : IEditOp
{
    public const string Type = "LutCube";
    public string OpType => Type;

    public string Path = "";
    public float Intensity = 1f;
    public LutInterpolation Interpolation = LutInterpolation.Tetrahedral;

    private int _size;
    private float[]? _table; // size^3 * 3, indexing order = ((b*size)+g)*size+r

    public bool IsIdentity => string.IsNullOrEmpty(Path) || Intensity < 1e-4f || _table == null;

    private void EnsureLoaded()
    {
        if (_table != null || string.IsNullOrEmpty(Path) || !File.Exists(Path)) return;
        try { Parse(Path); } catch { _table = null; }
    }

    public void Apply(LinearImage image, float scale)
    {
        EnsureLoaded();
        if (IsIdentity) return;
        int size = _size;
        float[] lut = _table!;
        float intensity = Math.Clamp(Intensity, 0f, 1f);
        var interp = Interpolation;

        image.ProcessPixels((ref float r, ref float g, ref float b, ref float a) =>
        {
            float sr = ColorSpace.LinearToSrgb(r), sg = ColorSpace.LinearToSrgb(g), sb = ColorSpace.LinearToSrgb(b);
            if (interp == LutInterpolation.Tetrahedral)
            {
                Tetrahedral(lut, size, sr, sg, sb, out float or, out float og, out float ob);
                sr += (or - sr) * intensity;
                sg += (og - sg) * intensity;
                sb += (ob - sb) * intensity;
            }
            else
            {
                Trilinear(lut, size, sr, sg, sb, out float or, out float og, out float ob);
                sr += (or - sr) * intensity;
                sg += (og - sg) * intensity;
                sb += (ob - sb) * intensity;
            }
            r = ColorSpace.SrgbToLinear(sr); g = ColorSpace.SrgbToLinear(sg); b = ColorSpace.SrgbToLinear(sb);
        });
    }

    /// <summary>
    /// High-precision Tetrahedral 3D interpolation delegating to ZeroGraphics.Imaging.Filters.ColorLut3D.
    /// </summary>
    public static void Tetrahedral(float[] lut, int size, float r, float g, float b,
        out float or, out float og, out float ob)
        => ZeroGraphics.Imaging.Filters.ColorLut3D.SampleTetrahedral(lut, size, r, g, b, out or, out og, out ob);

    /// <summary>
    /// Trilinear 3D interpolation delegating to ZeroGraphics.Imaging.Filters.ColorLut3D.
    /// </summary>
    public static void Trilinear(float[] lut, int size, float r, float g, float b,
        out float or, out float og, out float ob)
        => ZeroGraphics.Imaging.Filters.ColorLut3D.SampleTrilinear(lut, size, r, g, b, out or, out og, out ob);

    private static int Idx(int size, int r, int g, int b) => ((b * size + g) * size + r) * 3;

    private void Parse(string path)
    {
        int size = 0;
        var values = new List<float>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) int.TryParse(parts[1], out size);
                continue;
            }
            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DOMAIN_", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("LUT_1D_SIZE", StringComparison.OrdinalIgnoreCase))
                continue;
            var nums = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nums.Length >= 3 &&
                float.TryParse(nums[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rr) &&
                float.TryParse(nums[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gg) &&
                float.TryParse(nums[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var bb))
            {
                values.Add(rr); values.Add(gg); values.Add(bb);
            }
        }
        if (size > 0 && values.Count == size * size * size * 3)
        {
            _size = size;
            _table = values.ToArray();
        }
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["path"] = Path,
        ["intensity"] = Intensity.ToString("R", CultureInfo.InvariantCulture),
        ["interp"] = Interpolation.ToString(),
    };
    public static LutCubeOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        Path = EditOpRegistry.S(p, "path"),
        Intensity = EditOpRegistry.F(p, "intensity", 1f),
        Interpolation = p.TryGetValue("interp", out var ip) && Enum.TryParse<LutInterpolation>(ip, true, out var parsed)
            ? parsed
            : LutInterpolation.Tetrahedral,
    };
    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
