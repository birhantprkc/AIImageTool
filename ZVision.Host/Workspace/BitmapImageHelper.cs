using System;
using System.IO;
using System.Windows.Media.Imaging;
using ZVision.Shared;

namespace ZVision.Host.Workspace;

/// <summary>
/// Helper tải ảnh WPF BitmapImage có nhận diện và tự động áp dụng góc xoay EXIF Orientation.
/// Tránh lỗi ảnh chụp dọc bị xoay ngang khi xem bằng WPF Image control mặc định.
/// </summary>
public static class BitmapImageHelper
{
    /// <summary>
    /// Chuyển đổi mã EXIF Orientation (1..8) sang Rotation enum của WPF.
    /// EXIF 1: Chuẩn (Rotate0)
    /// EXIF 3: Xoay 180° (Rotate180)
    /// EXIF 6: Xoay 90° theo chiều kim đồng hồ (Rotate90)
    /// EXIF 8: Xoay 270° theo chiều kim đồng hồ (Rotate270)
    /// </summary>
    public static Rotation GetRotation(int orientation) => orientation switch
    {
        3 => Rotation.Rotate180,
        6 => Rotation.Rotate90,
        8 => Rotation.Rotate270,
        _ => Rotation.Rotate0
    };

    /// <summary>
    /// Tải file ảnh sang BitmapImage với xoay EXIF và Freeze để an toàn cross-thread.
    /// </summary>
    public static BitmapImage Load(string path, int decodePixelWidth = 0)
    {
        int orientation = 1;
        try
        {
            var meta = ExifReader.GetOrCreate(path);
            orientation = meta.Orientation ?? 1;
        }
        catch
        {
            // Fallback khi đọc metadata lỗi hoặc file không có EXIF
        }

        var rot = GetRotation(orientation);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        if (decodePixelWidth > 0)
        {
            bmp.DecodePixelWidth = decodePixelWidth;
        }
        if (rot != Rotation.Rotate0)
        {
            bmp.Rotation = rot;
        }
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
