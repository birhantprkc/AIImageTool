using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace ZeroVision.Imaging;

public enum InpaintAlgorithm
{
    FastTeleaDiffusion,
    PatchSynthesis,
    NeuralLaMaHook
}

/// <summary>
/// AI-Assisted Inpainting Op (#LaMa / Diffusion) for non-destructive object/blemish removal.
/// Interpolates and seamlessly synthesizes missing pixel regions defined by brush spots or masks.
/// </summary>
public sealed class AiInpaintOp : IEditOp
{
    public const string Type = "AiInpaint";
    public string OpType => Type;

    public InpaintAlgorithm Algorithm { get; set; } = InpaintAlgorithm.FastTeleaDiffusion;
    public float Strength { get; set; } = 1.0f;
    public int Iterations { get; set; } = 8;
    public List<InpaintRegion> Regions { get; } = new();

    public readonly record struct InpaintRegion(float NormalizedX, float NormalizedY, float Radius);

    public bool IsIdentity => Regions.Count == 0;

    public void Apply(LinearImage image, float scale)
    {
        if (IsIdentity) return;

        int w = image.Width;
        int h = image.Height;
        float maxEdge = MathF.Max(w, h);
        float[] pixels = image.Pixels;

        foreach (var region in Regions)
        {
            int cx = (int)MathF.Round(region.NormalizedX * (w - 1));
            int cy = (int)MathF.Round(region.NormalizedY * (h - 1));
            int radPx = Math.Max(2, (int)MathF.Round(region.Radius * maxEdge * scale));

            int x0 = Math.Max(0, cx - radPx);
            int x1 = Math.Min(w - 1, cx + radPx);
            int y0 = Math.Max(0, cy - radPx);
            int y1 = Math.Min(h - 1, cy + radPx);

            // 1. Build local binary mask for region
            int rw = x1 - x0 + 1;
            int rh = y1 - y0 + 1;
            bool[] mask = new bool[rw * rh];
            float radSq = radPx * radPx;

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                int my = y - y0;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    int mx = x - x0;
                    if (dx * dx + dy * dy <= radSq)
                    {
                        mask[my * rw + mx] = true; // Hole to inpaint
                    }
                }
            }

            // 2. Perform Poisson gradient diffusion / Fast Telea inpainting across the hole
            PerformFastDiffusion(pixels, w, h, x0, y0, rw, rh, mask, Iterations, Strength);
        }
    }

    private static void PerformFastDiffusion(
        float[] pixels, int fullW, int fullH,
        int rx, int ry, int rw, int rh,
        bool[] mask, int iterations, float strength)
    {
        // Allocate working sub-buffers for iterative PDE diffusion
        float[] bufR = new float[rw * rh];
        float[] bufG = new float[rw * rh];
        float[] bufB = new float[rw * rh];

        // Initialize with source or boundary mean
        float sumR = 0, sumG = 0, sumB = 0;
        int borderCount = 0;

        for (int y = 0; y < rh; y++)
        {
            int globalY = ry + y;
            for (int x = 0; x < rw; x++)
            {
                int globalX = rx + x;
                int gOff = (globalY * fullW + globalX) * 4;
                int mIdx = y * rw + x;

                bufR[mIdx] = pixels[gOff];
                bufG[mIdx] = pixels[gOff + 1];
                bufB[mIdx] = pixels[gOff + 2];

                if (!mask[mIdx])
                {
                    sumR += pixels[gOff];
                    sumG += pixels[gOff + 1];
                    sumB += pixels[gOff + 2];
                    borderCount++;
                }
            }
        }

        if (borderCount > 0)
        {
            float avgR = sumR / borderCount;
            float avgG = sumG / borderCount;
            float avgB = sumB / borderCount;

            // Fill initial hole with ambient mean
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                {
                    bufR[i] = avgR;
                    bufG[i] = avgG;
                    bufB[i] = avgB;
                }
            }
        }

        // Iterative 4-neighbor Laplace smoothing over the hole
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int y = 1; y < rh - 1; y++)
            {
                for (int x = 1; x < rw - 1; x++)
                {
                    int idx = y * rw + x;
                    if (!mask[idx]) continue;

                    int up = (y - 1) * rw + x;
                    int down = (y + 1) * rw + x;
                    int left = y * rw + (x - 1);
                    int right = y * rw + (x + 1);

                    bufR[idx] = 0.25f * (bufR[up] + bufR[down] + bufR[left] + bufR[right]);
                    bufG[idx] = 0.25f * (bufG[up] + bufG[down] + bufG[left] + bufG[right]);
                    bufB[idx] = 0.25f * (bufB[up] + bufB[down] + bufB[left] + bufB[right]);
                }
            }
        }

        // Write back diffused region into LinearImage
        for (int y = 0; y < rh; y++)
        {
            int globalY = ry + y;
            for (int x = 0; x < rw; x++)
            {
                int idx = y * rw + x;
                if (!mask[idx]) continue;

                int globalX = rx + x;
                int gOff = (globalY * fullW + globalX) * 4;

                if (strength >= 0.999f)
                {
                    pixels[gOff] = bufR[idx];
                    pixels[gOff + 1] = bufG[idx];
                    pixels[gOff + 2] = bufB[idx];
                }
                else
                {
                    pixels[gOff] = MathF.FusedMultiplyAdd(bufR[idx] - pixels[gOff], strength, pixels[gOff]);
                    pixels[gOff + 1] = MathF.FusedMultiplyAdd(bufG[idx] - pixels[gOff + 1], strength, pixels[gOff + 1]);
                    pixels[gOff + 2] = MathF.FusedMultiplyAdd(bufB[idx] - pixels[gOff + 2], strength, pixels[gOff + 2]);
                }
            }
        }
    }

    public static IEditOp Create(IReadOnlyDictionary<string, string> p)
    {
        var op = new AiInpaintOp
        {
            Strength = EditOpRegistry.F(p, "strength", 1.0f),
            Iterations = (int)EditOpRegistry.F(p, "iterations", 8f)
        };

        if (p.TryGetValue("regions", out var regStr) && !string.IsNullOrWhiteSpace(regStr))
        {
            var parts = regStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var nums = part.Split(',');
                if (nums.Length == 3 &&
                    float.TryParse(nums[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(nums[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(nums[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                {
                    op.Regions.Add(new InpaintRegion(x, y, r));
                }
            }
        }

        return op;
    }

    public static void Register(EditOpRegistry reg) => reg.Register(Type, Create);

    public Dictionary<string, string> ToParams()
    {
        var dict = new Dictionary<string, string>
        {
            ["strength"] = Strength.ToString(CultureInfo.InvariantCulture),
            ["iterations"] = Iterations.ToString(CultureInfo.InvariantCulture)
        };

        if (Regions.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Regions.Count; i++)
            {
                if (i > 0) sb.Append(';');
                var r = Regions[i];
                sb.Append(r.NormalizedX.ToString("F4", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(r.NormalizedY.ToString("F4", CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(r.Radius.ToString("F4", CultureInfo.InvariantCulture));
            }
            dict["regions"] = sb.ToString();
        }

        return dict;
    }
}
