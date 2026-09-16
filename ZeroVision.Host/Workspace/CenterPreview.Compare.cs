using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Editors;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

// Side-by-side & split-curtain before/after compare powered by ZeroUI CompareViewerControl (11.4).
public partial class CenterPreview
{
    private bool _compareMode;

    private void BtnCompare_Click(object sender, RoutedEventArgs e) => ToggleCompareMode();

    private void ToggleCompareMode()
    {
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path)) return;

        _compareMode = !_compareMode;
        if (_compareMode)
        {
            // Disable crop if active to avoid overlay conflict.
            if (_cropMode) ToggleCropMode();
            paneSingle.Visibility = Visibility.Collapsed;
            paneGrid.Visibility = Visibility.Collapsed;
            paneCull.Visibility = Visibility.Collapsed;
            paneFull.Visibility = Visibility.Collapsed;
            ctrlCompare.Visibility = Visibility.Visible;
            btnCompare.Background = ThemeManager.GetBrush("AccentBrush");
            ctrlCompare.Reset();
            _ = LoadCompareAsync(path);
        }
        else
        {
            ctrlCompare.Visibility = Visibility.Collapsed;
            ctrlCompare.Clear();
            btnCompare.Background = ThemeManager.GetBrush("BgHoverBrush");
            SwitchMode(LighttableMode.Single);
        }
    }

    /// <summary>Render original image (pointer=0) and edited image (current pointer) into CompareViewerControl.</summary>
    private async System.Threading.Tasks.Task LoadCompareAsync(string path)
    {
        // AFTER: edited image. BEFORE: original image.
        var ops = _history?.GetStack(path) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
        int pointer = _history?.GetPointer(path) ?? 0;

        if (_renderer.CanDecode(path) && pointer > 0)
        {
            try
            {
                var before = await _renderer.RenderPreviewAsync(path, ops, 0);
                var after = await _renderer.RenderPreviewAsync(path, ops, pointer);
                if (!_compareMode) return;
                if (before != null) ctrlCompare.BeforeSource = before;
                if (after != null) ctrlCompare.AfterSource = after;
                return;
            }
            catch { }
        }

        // Fallback: unedited or undecodable image -> both panes show original file from disk.
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 1600;
            bmp.EndInit();
            bmp.Freeze();
            ctrlCompare.BeforeSource = bmp;
            ctrlCompare.AfterSource = bmp;
        }
        catch { }
    }
}
