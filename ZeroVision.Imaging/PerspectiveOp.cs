using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using ZeroGraphics.Vision.Stitching;

namespace ZeroVision.Imaging;

/// <summary>
/// Perspective / Upright (keystone): hiệu chỉnh phối cảnh bằng phép biến đổi đồng nhất
/// (homography 3x3) sử dụng engine ZeroGraphics.Vision.Stitching.Homography2D.
/// Hai tham số trực quan Vertical / Horizontal mô phỏng nghiêng máy
/// (chuẩn Perspective Correction): Vertical &gt; 0 kéo đỉnh ra (sửa nhà bị "ngả sau"),
/// Horizontal xoay quanh trục dọc. Rotate xoay phẳng, Scale phóng để bù viền đen.
///
/// Là IResizingOp (giữ nguyên W×H nhưng remap pixel). Toạ độ chuẩn hoá nên khớp proxy/full-res.
/// Lấy mẫu ngược (inverse-map) song tuyến; ngoài biên = trong suốt.
/// </summary>
public sealed class PerspectiveOp : IResizingOp
{
    public const string Type = "Perspective";
    public string OpType => Type;

    public float Vertical;     // [-1..1] keystone dọc
    public float Horizontal;   // [-1..1] keystone ngang
    public float Rotate;       // độ, xoay phẳng
    public float Scale = 1f;   // phóng để bù viền

    public bool IsIdentity =>
        Near(Vertical) && Near(Horizontal) && Near(Rotate) && MathF.Abs(Scale - 1f) < 1e-4f;
    private static bool Near(float v) => MathF.Abs(v) < 1e-4f;

    public void Apply(LinearImage image, float scale) { /* resizing op */ }

    public LinearImage ApplyResize(LinearImage img, float scale)
    {
        if (IsIdentity) return img;
        int w = img.Width, h = img.Height;
        var dst = new LinearImage(w, h);
        float[] s = img.Pixels, d = dst.Pixels;

        // Dựng homography forward (toạ độ chuẩn hoá tâm gốc [-1..1]) qua Homography2D
        Homography2D fwd = BuildForward(Vertical, Horizontal, Rotate, Scale);
        Homography2D inv = fwd.Invert();

        float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
        float half = MathF.Max(cx, cy);

        Parallel.For(0, h, dy =>
        {
            for (int dx = 0; dx < w; dx++)
            {
                double nx = (dx - cx) / half;
                double ny = (dy - cy) / half;
                PointF pt = inv.TransformPoint(nx, ny);
                float sx = (float)(pt.X * half + cx);
                float sy = (float)(pt.Y * half + cy);
                Sample(s, w, h, sx, sy, d, (dy * w + dx) * 4);
            }
        });
        return dst;
    }

    // Forward map qua ZeroGraphics.Vision.Stitching.Homography2D
    private static Homography2D BuildForward(float vert, float horiz, float rotDeg, float scl)
    {
        Homography2D m = Homography2D.Identity;

        // Xoay phẳng
        if (MathF.Abs(rotDeg) > 1e-4f)
        {
            double a = rotDeg * Math.PI / 180.0;
            double c = Math.Cos(a), s = Math.Sin(a);
            var r = new Homography2D(c, -s, 0, s, c, 0, 0, 0, 1);
            m = r.Multiply(m);
        }

        // Keystone: projective component
        const double kp = 0.5;
        var p = new Homography2D(1, 0, 0, 0, 1, 0, horiz * kp, vert * kp, 1);
        m = p.Multiply(m);

        // Phóng để bù viền
        if (MathF.Abs(scl - 1f) > 1e-5f && scl > 1e-3f)
        {
            var sMat = new Homography2D(scl, 0, 0, 0, scl, 0, 0, 0, 1);
            m = sMat.Multiply(m);
        }
        return m;
    }

    /// <summary>
    /// Computes the 3x3 homography matrix rectifying a 4-point quadrilateral into a rectangular box
    /// using ZeroGraphics.Vision.Stitching.Homography2D.Estimate (Direct Linear Transformation).
    /// </summary>
    public static Homography2D EstimateQuadRectification(IReadOnlyList<PointF> srcQuad, float dstWidth, float dstHeight)
    {
        var dstQuad = new PointF[]
        {
            new(0, 0),
            new(dstWidth, 0),
            new(dstWidth, dstHeight),
            new(0, dstHeight),
        };
        return Homography2D.Estimate(srcQuad, dstQuad);
    }

    private static void ClearPixel(float[] d, int o)
    {
        d[o] = 0; d[o + 1] = 0; d[o + 2] = 0; d[o + 3] = 0;
    }

    private static void Sample(float[] s, int sw, int sh, float fx, float fy, float[] d, int do_)
    {
        if (fx < 0 || fy < 0 || fx > sw - 1 || fy > sh - 1)
        {
            ClearPixel(d, do_);
            return;
        }
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Math.Min(sw - 1, x0 + 1), y1 = Math.Min(sh - 1, y0 + 1);
        float tx = fx - x0, ty = fy - y0;
        for (int c = 0; c < 4; c++)
        {
            float p00 = s[(y0 * sw + x0) * 4 + c];
            float p10 = s[(y0 * sw + x1) * 4 + c];
            float p01 = s[(y1 * sw + x0) * 4 + c];
            float p11 = s[(y1 * sw + x1) * 4 + c];
            float top = p00 + (p10 - p00) * tx;
            float bot = p01 + (p11 - p01) * tx;
            d[do_ + c] = top + (bot - top) * ty;
        }
    }

    public Dictionary<string, string> ToParams() => new()
    {
        ["vert"] = F(Vertical), ["horiz"] = F(Horizontal), ["rotate"] = F(Rotate), ["scale"] = F(Scale),
    };
    private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    public static PerspectiveOp FromParams(IReadOnlyDictionary<string, string> p) => new()
    {
        Vertical = EditOpRegistry.F(p, "vert"),
        Horizontal = EditOpRegistry.F(p, "horiz"),
        Rotate = EditOpRegistry.F(p, "rotate"),
        Scale = EditOpRegistry.F(p, "scale", 1f),
    };
    public static void Register(EditOpRegistry reg) => reg.Register(Type, FromParams);
}
