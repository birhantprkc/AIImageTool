using System;
using System.Collections.Generic;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;

namespace ZeroVision.Imaging;

/// <summary>
/// Tự cân bằng đường chân trời / phương đứng (auto-straighten):
/// Ước lượng góc nghiêng dominant kết hợp mô hình RANSAC sub-pixel (ZeroGraphics.Vision)
/// với fallback TRUNG BÌNH HƯỚNG (circular mean) của vector gradient.
/// </summary>
public static class AutoStraighten
{
    /// <summary>
    /// Ước lượng góc nghiêng (độ, [-45..45]). Trả 0 nếu không có hướng dominant rõ. Góc dương = nội dung
    /// nghiêng theo chiều kim đồng hồ; UI đặt straighten = -angle để cân bằng.
    /// </summary>
    public static float EstimateAngle(LinearImage img, float maxAngleDeg = 45f)
    {
        if (img == null) return 0f;
        int w = img.Width, h = img.Height;
        if (w < 8 || h < 8) return 0f;

        // Làm mờ vừa phải (~3px ở full-res; scale-independent vì ta chỉ cần hướng) để de-alias.
        float radius = MathF.Max(2f, MathF.Min(w, h) / 40f);
        float[] lum = GaussianBlur.BlurLuminance(img, radius);

        float maxA = Math.Clamp(maxAngleDeg, 1f, 45f);

        // Pass 1: tính ngưỡng gradient trung bình.
        double sumMag = 0; long n = 0;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                Sobel(lum, w, x, y, out float gx, out float gy);
                sumMag += MathF.Sqrt(gx * gx + gy * gy);
                n++;
            }
        if (n == 0) return 0f;
        float mean = (float)(sumMag / n);
        float thr = mean * 2f;
        if (thr < 1e-6f) return 0f;
        float thrSq = thr * thr;

        // Pass 1.5: Trích xuất các điểm biên có gradient mạnh cho RANSAC Line Fitting (ZeroGraphics.Vision)
        var edgePoints = new List<VisionPoint2D>();
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                Sobel(lum, w, x, y, out float gx, out float gy);
                float mag2 = gx * gx + gy * gy;
                if (mag2 >= thrSq)
                {
                    edgePoints.Add(new VisionPoint2D(x, y));
                }
            }
        }

        // Nếu mật độ điểm biên đủ dày, chạy RANSAC để trích xuất đường thẳng dominant (chân trời, kiến trúc)
        if (edgePoints.Count >= 20)
        {
            try
            {
                var ransac = RansacFitter.FitLineRansac(
                    edgePoints,
                    distanceThreshold: 1.5,
                    maxIterations: 100,
                    minInlierRatio: 0.35,
                    seed: 42);

                if (ransac.InlierRatio >= 0.35 && ransac.InlierCount >= 15)
                {
                    float lineAngle = (float)ransac.Model.AngleDegrees;
                    float a = Mod90To45(lineAngle);
                    if (MathF.Abs(a) <= maxA)
                    {
                        if (MathF.Abs(a) < 0.05f) return 0f;
                        return Math.Clamp(a, -maxA, maxA);
                    }
                }
            }
            catch
            {
                // Fallback xuống Pass 2 circular mean nếu RANSAC không hội tụ
            }
        }

        // Pass 2: vector trung bình của góc-nhân-đôi (chu kỳ 90° -> nhân 2 thành chu kỳ 180° -> ×2 rad).
        double sx = 0, sy = 0; double totalW = 0;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                Sobel(lum, w, x, y, out float gx, out float gy);
                float mag2 = gx * gx + gy * gy;
                if (mag2 < thr * thr) continue;
                float gradAngle = MathF.Atan2(gy, gx) * 180f / MathF.PI;
                float a = Mod90To45(gradAngle - 90f); // hướng cạnh trong [-45..45]
                if (a < -maxA || a > maxA) continue;
                double w2 = MathF.Sqrt(mag2);          // trọng số = |gradient|
                double phi = a * 2.0 * Math.PI / 90.0; // map [-45..45]->[-π..π]
                sx += w2 * Math.Cos(phi);
                sy += w2 * Math.Sin(phi);
                totalW += w2;
            }

        if (totalW <= 0) return 0f;
        // "Độ tập trung" của hướng: |vector|/Σw. Thấp -> không có hướng dominant -> trả 0.
        double concentration = Math.Sqrt(sx * sx + sy * sy) / totalW;
        if (concentration < 0.15) return 0f;

        double meanAngle = Math.Atan2(sy, sx) / 2.0 * 90.0 / Math.PI; // về lại [-45..45]
        float angle = (float)meanAngle;
        if (MathF.Abs(angle) < 0.05f) return 0f;
        return Math.Clamp(angle, -maxA, maxA);
    }

    private static void Sobel(float[] p, int w, int x, int y, out float gx, out float gy)
    {
        int o = y * w + x;
        float tl = p[o - w - 1], tc = p[o - w], tr = p[o - w + 1];
        float ml = p[o - 1], mr = p[o + 1];
        float bl = p[o + w - 1], bc = p[o + w], br = p[o + w + 1];
        gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
        gy = (bl + 2 * bc + br) - (tl + 2 * tc + tr);
    }

    private static float Mod90To45(float deg)
    {
        float a = deg % 90f;
        if (a < 0) a += 90f;
        if (a > 45f) a -= 90f;
        return a;
    }
}
