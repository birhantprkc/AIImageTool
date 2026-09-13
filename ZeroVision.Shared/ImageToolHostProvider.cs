namespace ZeroVision.Shared;

/// <summary>
/// Holder cho IImageToolHost của MainWindow. Plugin inject IImageToolHostProvider
/// và gọi .Host khi cần (lazy, vì host chỉ tồn tại sau khi MainWindow show).
/// </summary>
public class ImageToolHostProvider
{
    public ZeroVision.Core.IImageToolHost? Host { get; set; }
}
