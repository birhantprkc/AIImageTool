namespace ZeroVision.Core;

/// <summary>
/// Quản lý đường dẫn và định danh cho Virtual Copy (bản sao ảo không tốn dung lượng ổ đĩa).
/// Quy ước hậu tố: #vcN (ví dụ: C:\Photos\DSC_0001.JPG#vc1).
/// </summary>
public static class VirtualCopyHelper
{
    public const string Suffix = "#vc";

    /// <summary>Kiểm tra xem đường dẫn có phải là bản sao ảo hay không.</summary>
    public static bool IsVirtualCopy(string? path) =>
        !string.IsNullOrEmpty(path) && path.Contains(Suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Bóc tách đường dẫn file gốc thực tế trên đĩa vật lý.</summary>
    public static string ResolveDiskPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        int idx = path.IndexOf(Suffix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? path[..idx] : path;
    }

    /// <summary>Lấy số thứ tự của bản sao ảo (ví dụ: #vc1 -> 1).</summary>
    public static string GetVirtualCopyNumber(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        int idx = path.IndexOf(Suffix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? path[(idx + Suffix.Length)..] : "";
    }
}
