using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Editors;
using ZVision.Core;

namespace ZVision.Host.Workspace;

// Side-by-side & split-curtain before/after compare + reference view powered by ZeroUI CompareViewerControl (11.4).
public partial class CenterPreview
{
    private bool _compareMode;
    private bool _referenceMode;

    public bool IsCompareModeActive => _compareMode;
    public bool IsReferenceModeActive => _referenceMode;
    public string? ReferenceImagePath => _referenceImagePath;

    private void BtnCompare_Click(object sender, RoutedEventArgs e) => ToggleCompareMode();
    private void BtnReference_Click(object sender, RoutedEventArgs e) => ToggleReferenceMode();

    public void ToggleCompareMode()
    {
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path)) return;

        if (_referenceMode)
        {
            _referenceMode = false;
            if (btnReference != null) btnReference.Background = ThemeManager.GetBrush("BgHoverBrush");
        }

        _compareMode = !_compareMode;
        if (_compareMode)
        {
            if (_cropMode) ToggleCropMode();
            paneSingle.Visibility = Visibility.Collapsed;
            paneGrid.Visibility = Visibility.Collapsed;
            paneCull.Visibility = Visibility.Collapsed;
            paneFull.Visibility = Visibility.Collapsed;
            ctrlCompare.Visibility = Visibility.Visible;
            ctrlCompare.BeforeLabel = "BEFORE";
            ctrlCompare.AfterLabel = "AFTER";
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

    /// <summary>Toggle Reference View (Shift+R): lock reference photo on left, active photo on right.</summary>
    public void ToggleReferenceMode(string? refPath = null)
    {
        var active = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(active)) return;

        if (!string.IsNullOrEmpty(refPath))
            _referenceImagePath = refPath;
        else if (string.IsNullOrEmpty(_referenceImagePath))
            _referenceImagePath = active;

        if (_compareMode)
        {
            _compareMode = false;
            btnCompare.Background = ThemeManager.GetBrush("BgHoverBrush");
        }

        _referenceMode = !_referenceMode;
        if (_referenceMode)
        {
            if (_cropMode) ToggleCropMode();
            paneSingle.Visibility = Visibility.Collapsed;
            paneGrid.Visibility = Visibility.Collapsed;
            paneCull.Visibility = Visibility.Collapsed;
            paneFull.Visibility = Visibility.Collapsed;
            ctrlCompare.Visibility = Visibility.Visible;
            ctrlCompare.BeforeLabel = "REFERENCE";
            ctrlCompare.AfterLabel = "ACTIVE";
            if (btnReference != null) btnReference.Background = ThemeManager.GetBrush("AccentBrush");
            ctrlCompare.Reset();
            _ = LoadReferenceAsync(_referenceImagePath!, active);
        }
        else
        {
            ctrlCompare.Visibility = Visibility.Collapsed;
            ctrlCompare.Clear();
            if (btnReference != null) btnReference.Background = ThemeManager.GetBrush("BgHoverBrush");
            SwitchMode(LighttableMode.Single);
        }
    }

    /// <summary>Explicitly designate an image as reference photo and switch to Reference Mode.</summary>
    public void SetReferenceImage(string path)
    {
        _referenceImagePath = path;
        if (_referenceMode)
        {
            var active = _workspace?.ActiveImage;
            if (!string.IsNullOrEmpty(active))
                _ = LoadReferenceAsync(_referenceImagePath, active);
        }
        else
        {
            ToggleReferenceMode(path);
        }
    }

    /// <summary>Called by pipeline after rendering active photo to update CompareViewerControl right pane live.</summary>
    public void RefreshCompareAfter(System.Windows.Media.ImageSource after)
    {
        if (_compareMode || _referenceMode)
        {
            ctrlCompare.AfterSource = after;
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
            var bmp = LoadDiskBitmap(path);
            ctrlCompare.BeforeSource = bmp;
            ctrlCompare.AfterSource = bmp;
        }
        catch { }
    }

    /// <summary>Render reference image into BeforeSource and active image into AfterSource.</summary>
    private async System.Threading.Tasks.Task LoadReferenceAsync(string refPath, string activePath)
    {
        // 1. Load Reference image into left pane (BeforeSource)
        var refOps = _history?.GetStack(refPath) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
        int refPointer = _history?.GetPointer(refPath) ?? 0;
        System.Windows.Media.ImageSource? refBmp = null;
        if (_renderer.CanDecode(refPath))
        {
            try { refBmp = await _renderer.RenderPreviewAsync(refPath, refOps, refPointer); } catch { }
        }
        refBmp ??= LoadDiskBitmap(refPath);

        if (!_referenceMode) return;
        if (refBmp != null) ctrlCompare.BeforeSource = refBmp;

        // 2. Load Active image into right pane (AfterSource)
        var actOps = _history?.GetStack(activePath) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
        int actPointer = _history?.GetPointer(activePath) ?? 0;
        System.Windows.Media.ImageSource? actBmp = null;
        if (_renderer.CanDecode(activePath))
        {
            try { actBmp = await _renderer.RenderPreviewAsync(activePath, actOps, actPointer); } catch { }
        }
        actBmp ??= LoadDiskBitmap(activePath);

        if (!_referenceMode) return;
        if (actBmp != null) ctrlCompare.AfterSource = actBmp;
    }

    private static BitmapImage? LoadDiskBitmap(string path)
    {
        try
        {
            return BitmapImageHelper.Load(path, decodePixelWidth: 1600);
        }
        catch
        {
            return null;
        }
    }
}
