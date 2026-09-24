namespace ZVision.Shared;

/// <summary>
/// Holder cho IImageToolHost của MainWindow. Plugin inject IImageToolHostProvider
/// và gọi .Host khi cần (lazy, vì host chỉ tồn tại sau khi MainWindow show).
/// </summary>
public class ImageToolHostProvider
{
    public ZVision.Core.IImageToolHost? Host { get; set; }
}
