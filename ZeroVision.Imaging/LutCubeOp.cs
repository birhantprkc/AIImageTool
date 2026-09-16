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
    /// High-precision Tetrahedral 3D interpolation. Splits each cube into 6 tetrahedra,
    /// evaluating only 4 corners per point and guaranteeing smooth neutral diagonal tracking.
    /// </summary>
    public static void Tetrahedral(float[] lut, int size, float r, float g, float b,
        out float or, out float og, out float ob)
    {
        float fr = Math.Clamp(r, 0f, 1f) * (size - 1);
        float fg = Math.Clamp(g, 0f, 1f) * (size - 1);
        float fb = Math.Clamp(b, 0f, 1f) * (size - 1);
        int r0 = (int)fr, g0 = (int)fg, b0 = (int)fb;
        int r1 = Math.Min(size - 1, r0 + 1), g1 = Math.Min(size - 1, g0 + 1), b1 = Math.Min(size - 1, b0 + 1);
        float dr = fr - r0, dg = fg - g0, db = fb - b0;

        int i000 = Idx(size, r0, g0, b0);
        int i111 = Idx(size, r1, g1, b1);
        int iA, iB;
        float w0, wA, wB, w1;

        // Determine which of the 6 tetrahedra contains (dr, dg, db)
        if (dr >= dg)
        {
            if (dg >= db)
            {
                // dr >= dg >= db
                iA = Idx(size, r1, g0, b0);
                iB = Idx(size, r1, g1, b0);
                w0 = 1f - dr;
                wA = dr - dg;
                wB = dg - db;
                w1 = db;
            }
            else if (dr >= db)
            {
                // dr >= db > dg
                iA = Idx(size, r1, g0, b0);
                iB = Idx(size, r1, g0, b1);
                w0 = 1f - dr;
                wA = dr - db;
                wB = db - dg;
                w1 = dg;
            }
            else
            {
                // db > dr >= dg
                iA = Idx(size, r0, g0, b1);
                iB = Idx(size, r1, g0, b1);
                w0 = 1f - db;
                wA = db - dr;
                wB = dr - dg;
                w1 = dg;
            }
        }
        else
        {
            if (db > dg)
            {
                // db > dg > dr
                iA = Idx(size, r0, g0, b1);
                iB = Idx(size, r0, g1, b1);
                w0 = 1f - db;
                wA = db - dg;
                wB = dg - dr;
                w1 = dr;
            }
            else if (db > dr)
            {
                // dg >= db > dr
                iA = Idx(size, r0, g1, b0);
                iB = Idx(size, r0, g1, b1);
                w0 = 1f - dg;
                wA = dg - db;
                wB = db - dr;
                w1 = dr;
            }
            else
            {
                // dg > dr >= db
                iA = Idx(size, r0, g1, b0);
                iB = Idx(size, r1, g1, b0);
                w0 = 1f - dg;
                wA = dg - dr;
                wB = dr - db;
                w1 = db;
            }
        }

        or = w0 * lut[i000]     + wA * lut[iA]     + wB * lut[iB]     + w1 * lut[i111];
        og = w0 * lut[i000 + 1] + wA * lut[iA + 1] + wB * lut[iB + 1] + w1 * lut[i111 + 1];
        ob = w0 * lut[i000 + 2] + wA * lut[iA + 2] + wB * lut[iB + 2] + w1 * lut[i111 + 2];
    }

    public static void Trilinear(float[] lut, int size, float r, float g, float b,
        out float or, out float og, out float ob)
    {
        float fr = Math.Clamp(r, 0f, 1f) * (size - 1);
        float fg = Math.Clamp(g, 0f, 1f) * (size - 1);
        float fb = Math.Clamp(b, 0f, 1f) * (size - 1);
        int r0 = (int)fr, g0 = (int)fg, b0 = (int)fb;
        int r1 = Math.Min(size - 1, r0 + 1), g1 = Math.Min(size - 1, g0 + 1), b1 = Math.Min(size - 1, b0 + 1);
        float dr = fr - r0, dg = fg - g0, db = fb - b0;

        or = og = ob = 0f;
        for (int c = 0; c < 3; c++)
        {
            float c000 = lut[Idx(size, r0, g0, b0) + c];
            float c100 = lut[Idx(size, r1, g0, b0) + c];
            float c010 = lut[Idx(size, r0, g1, b0) + c];
            float c110 = lut[Idx(size, r1, g1, b0) + c];
            float c001 = lut[Idx(size, r0, g0, b1) + c];
            float c101 = lut[Idx(size, r1, g0, b1) + c];
            float c011 = lut[Idx(size, r0, g1, b1) + c];
            float c111 = lut[Idx(size, r1, g1, b1) + c];
            float c00 = c000 + (c100 - c000) * dr;
            float c10 = c010 + (c110 - c010) * dr;
            float c01 = c001 + (c101 - c001) * dr;
            float c11 = c011 + (c111 - c011) * dr;
            float c0 = c00 + (c10 - c00) * dg;
            float c1 = c01 + (c11 - c01) * dg;
            float val = c0 + (c1 - c0) * db;
            if (c == 0) or = val; else if (c == 1) og = val; else ob = val;
        }
    }

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
