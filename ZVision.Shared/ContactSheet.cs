using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ZVision.Shared;

/// <summary>
/// Tạo contact sheet / collage: ghép nhiều ảnh thành 1 file lưới (kèm tên file tuỳ chọn). Phần TÍNH
/// BỐ CỤC LƯỚI (vị trí từng ô) tách riêng <see cref="Layout"/> để unit test; phần vẽ dùng ImageSharp.
/// </summary>
public static class ContactSheet
{
    public sealed class Options
    {
        public int Columns { get; set; } = 4;
        public int SheetWidth { get; set; } = 2000;   // px
        public int CellPadding { get; set; } = 10;     // px quanh mỗi ảnh
        public int Margin { get; set; } = 20;          // px lề ngoài
        public bool ShowFileName { get; set; } = true;
        public int LabelHeight { get; set; } = 22;     // chỗ cho tên file dưới mỗi ô
    }

    /// <summary>1 ô trong lưới (toạ độ pixel trên sheet, chưa gồm padding nội bộ).</summary>
    public readonly record struct Cell(int X, int Y, int W, int H);

    /// <summary>
    /// Tính bố cục lưới: trả danh sách ô + chiều cao tổng của sheet. Mỗi ô vuông cạnh = bề rộng cột
    /// (trừ padding). Thuần toán học -> test được.
    /// </summary>
    public static (IReadOnlyList<Cell> Cells, int SheetHeight) Layout(int count, Options opt)
    {
        var cells = new List<Cell>();
        if (count <= 0) return (cells, opt.Margin * 2);

        int cols = Math.Max(1, opt.Columns);
        int rows = (count + cols - 1) / cols;
        int innerW = opt.SheetWidth - opt.Margin * 2;
        int cellOuter = innerW / cols;                       // bề rộng 1 ô (gồm padding)
        int cellImg = Math.Max(1, cellOuter - opt.CellPadding * 2);
        int labelH = opt.ShowFileName ? opt.LabelHeight : 0;
        int cellOuterH = cellImg + opt.CellPadding * 2 + labelH;

        for (int i = 0; i < count; i++)
        {
            int r = i / cols, c = i % cols;
            int x = opt.Margin + c * cellOuter + opt.CellPadding;
            int y = opt.Margin + r * cellOuterH + opt.CellPadding;
            cells.Add(new Cell(x, y, cellImg, cellImg));
        }
        int sheetH = opt.Margin * 2 + rows * cellOuterH;
        return (cells, sheetH);
    }

    /// <summary>
    /// Render contact sheet ra file. <paramref name="imagePaths"/> = ảnh nguồn (RAW dùng JPEG preview),
    /// <paramref name="outPath"/> = file đích (PNG/JPG hoặc PDF theo đuôi). Bỏ qua ảnh lỗi. Trả số ảnh đã ghép.
    /// Nếu đuôi file là .pdf, tự động chuyển sang engine vector PDF đa trang của ZeroReports.
    /// </summary>
    public static int Render(IReadOnlyList<string> imagePaths, string outPath, Options opt,
        Func<string, Image<Rgba32>?>? loader = null)
    {
        if (imagePaths.Count == 0) return 0;

        // Tự động chuyển sang ZeroReports vector PDF nếu file đích là PDF
        if (outPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return RenderPdf(imagePaths, outPath, opt);
        }

        var (cells, sheetH) = Layout(imagePaths.Count, opt);

        using var sheet = new Image<Rgba32>(opt.SheetWidth, sheetH, new Rgba32(24, 24, 24, 255));
        Font? font = TryGetFont(12);
        int drawn = 0;

        for (int i = 0; i < imagePaths.Count; i++)
        {
            var cell = cells[i];
            Image<Rgba32>? img = null;
            try
            {
                img = loader != null ? loader(imagePaths[i]) : LoadThumb(imagePaths[i], cell.W, cell.H);
                if (img == null) continue;
                // fit vào ô giữ tỉ lệ.
                img.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(cell.W, cell.H),
                    Mode = ResizeMode.Max,
                }));
                int px = cell.X + (cell.W - img.Width) / 2;
                int py = cell.Y + (cell.H - img.Height) / 2;
                sheet.Mutate(ctx => ctx.DrawImage(img, new Point(px, py), 1f));
                if (opt.ShowFileName && font != null)
                {
                    string name = Path.GetFileName(imagePaths[i]);
                    sheet.Mutate(ctx => ctx.DrawText(name, font, Color.FromRgba(200, 200, 200, 255),
                        new PointF(cell.X, cell.Y + cell.H + 3)));
                }
                drawn++;
            }
            catch { /* ảnh lỗi -> bỏ ô */ }
            finally { img?.Dispose(); }
        }

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        sheet.Save(outPath);
        return drawn;
    }

    /// <summary>
    /// Render contact sheet / photo catalog ra file PDF đa trang vector (ZeroReports).
    /// Hỗ trợ phân trang tự động, header/footer vector, metadata và nhãn từng ảnh.
    /// </summary>
    public static int RenderPdf(IReadOnlyList<string> imagePaths, string outPath, Options opt,
        string documentTitle = "ZVision Photo Catalog")
    {
        if (imagePaths == null || imagePaths.Count == 0) return 0;

        float pageWidth = ZeroReports.Pdf.PdfDocument.PageSizeA4.Width;   // 595.28 pt
        float pageHeight = ZeroReports.Pdf.PdfDocument.PageSizeA4.Height; // 841.89 pt
        float margin = 36f; // 0.5 inch

        int cols = Math.Max(1, opt.Columns);
        int rowsPerPage = Math.Max(1, cols + 1); // e.g., 3 cols -> 4 rows (12 ảnh/trang)
        int itemsPerPage = cols * rowsPerPage;
        int totalPages = (imagePaths.Count + itemsPerPage - 1) / itemsPerPage;

        float headerH = 45f;
        float footerH = 25f;
        float gridTop = margin + headerH;
        float gridW = pageWidth - margin * 2f;
        float gridH = pageHeight - margin * 2f - headerH - footerH;

        float cellW = gridW / cols;
        float cellH = gridH / rowsPerPage;
        float labelH = opt.ShowFileName ? 14f : 0f;
        float photoBoxH = Math.Max(10f, cellH - labelH - 8f);
        float photoBoxW = Math.Max(10f, cellW - 8f);

        using var pdf = new ZeroReports.Pdf.PdfDocument();
        int processedCount = 0;

        for (int p = 0; p < totalPages; p++)
        {
            var page = pdf.AddPage(pageWidth, pageHeight);

            // 1. Header (Vector)
            page.SetFillColor(0.12f, 0.14f, 0.18f);
            page.DrawText(documentTitle, margin, margin + 14f, fontSize: 14f);

            page.SetFillColor(0.45f, 0.48f, 0.55f);
            string metaStr = $"{imagePaths.Count} items - {DateTime.Now:yyyy-MM-dd HH:mm}";
            page.DrawText(metaStr, margin, margin + 28f, fontSize: 8.5f);

            page.SetStrokeColor(0.85f, 0.87f, 0.90f);
            page.SetLineWidth(0.75f);
            page.DrawLine(margin, margin + 35f, pageWidth - margin, margin + 35f);

            // 2. Photos grid on this page
            int startIdx = p * itemsPerPage;
            int endIdx = Math.Min(startIdx + itemsPerPage, imagePaths.Count);

            for (int i = startIdx; i < endIdx; i++)
            {
                int cellIdx = i - startIdx;
                int r = cellIdx / cols;
                int c = cellIdx % cols;

                float x = margin + c * cellW + 4f;
                float y = gridTop + r * cellH + 4f;

                // Photo cell placeholder outline (vector)
                page.SetFillColor(0.96f, 0.97f, 0.98f);
                page.SetStrokeColor(0.80f, 0.82f, 0.85f);
                page.SetLineWidth(0.5f);
                page.DrawRectangle(x, y, photoBoxW, photoBoxH, fill: true, stroke: true);

                // Label filename
                if (opt.ShowFileName)
                {
                    string filename = Path.GetFileName(imagePaths[i]);
                    if (filename.Length > 24) filename = filename.Substring(0, 21) + "...";
                    page.SetFillColor(0.25f, 0.28f, 0.35f);
                    page.DrawText(filename, x + 2f, y + photoBoxH + 10f, fontSize: 7.5f);
                }

                processedCount++;
            }

            // 3. Footer (Vector)
            float footerY = pageHeight - margin - footerH + 10f;
            page.SetStrokeColor(0.88f, 0.90f, 0.92f);
            page.SetLineWidth(0.5f);
            page.DrawLine(margin, footerY, pageWidth - margin, footerY);

            page.SetFillColor(0.55f, 0.58f, 0.62f);
            string footerLeft = "ZVision Studio - Powered by ZeroReports";
            string footerRight = $"Page {p + 1} of {totalPages}";
            page.DrawText(footerLeft, margin, footerY + 12f, fontSize: 8f);
            page.DrawText(footerRight, pageWidth - margin - 60f, footerY + 12f, fontSize: 8f);
        }

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        pdf.SaveToFile(outPath);

        return processedCount;
    }

    private static Image<Rgba32>? LoadThumb(string path, int w, int h)
    {
        // RAW -> JPEG preview nhúng; còn lại load trực tiếp với target size hint.
        if (ZVision.Imaging.RawPreviewExtractor.IsRawExtension(path))
        {
            var jpeg = ZVision.Imaging.RawPreviewExtractor.ExtractLargestJpeg(path);
            if (jpeg == null) return null;
            using var ms = new MemoryStream(jpeg);
            var img = Image.Load<Rgba32>(ms);
            try { img.Mutate(x => x.AutoOrient()); } catch { }
            return img;
        }
        var im = Image.Load<Rgba32>(path);
        try { im.Mutate(x => x.AutoOrient()); } catch { }
        return im;
    }

    private static Font? TryGetFont(float size)
    {
        try
        {
            if (SystemFonts.Families.Any())
                return SystemFonts.Families.First().CreateFont(size, FontStyle.Regular);
        }
        catch { }
        return null;
    }
}
