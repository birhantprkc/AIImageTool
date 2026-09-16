using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Editors;

namespace ZeroVision.Host.Workspace;

public partial class CenterPreview
{
    private DevelopPanel? _developPanel;
    private bool _cropMode;
    private float _cropX, _cropY, _cropW = 1f, _cropH = 1f;
    // Guide overlay khi crop: 0=Thirds, 1=Golden ratio, 2=Diagonals, 3=Grid, 4=None.
    private int _cropGuide;
    private bool _cropBoxInitialized;

    public bool IsCropMode => _cropMode;

    /// <summary>Toggle crop mode.</summary>
    public void ToggleCrop() => ToggleCropMode();

    /// <summary>Swap crop aspect orientation between landscape and portrait (X key Lightroom style).</summary>
    public void SwapCropOrientation()
    {
        if (!_cropMode) return;
        int imgW = 0, imgH = 0;
        if (imgPreview.Source is BitmapSource bs)
        {
            imgW = bs.PixelWidth;
            imgH = bs.PixelHeight;
        }
        var r = ZeroVision.Imaging.CropAspect.SwapOrientation(imgW, imgH, _cropX, _cropY, _cropW, _cropH);
        _cropX = r.X;
        _cropY = r.Y;
        _cropW = r.W;
        _cropH = r.H;

        DrawCropOverlay();
        _developPanel?.SetCropRect(_cropX, _cropY, _cropW, _cropH);
    }

    /// <summary>Cycle crop guide overlay style (O key Lightroom style). Only effective when cropping.</summary>
    public void CycleCropGuide()
    {
        _cropGuide = (_cropGuide + 1) % 5;
        ctrlCropBox.GuideMode = (CropGuideMode)_cropGuide;
    }

    /// <summary>Bind DevelopPanel for two-way crop rectangle synchronization.</summary>
    public void BindCropPanel(DevelopPanel panel)
    {
        _developPanel = panel;
        _developPanel.CropChanged += (s, c) =>
        {
            _cropX = c.X; _cropY = c.Y; _cropW = c.W; _cropH = c.H;
            if (_cropMode) DrawCropOverlay();
        };

        if (!_cropBoxInitialized)
        {
            _cropBoxInitialized = true;
            ctrlCropBox.CropRectChanged += (s, r) =>
            {
                _cropX = (float)r.X;
                _cropY = (float)r.Y;
                _cropW = (float)r.Width;
                _cropH = (float)r.Height;
                _developPanel?.SetCropRect(_cropX, _cropY, _cropW, _cropH);
            };
        }

        // Populate aspect ratio presets into combobox once.
        if (cmbCropRatio.Items.Count == 0)
        {
            foreach (var p in ZeroVision.Imaging.CropAspect.Presets)
                cmbCropRatio.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = p.Name, Tag = p });
            cmbCropRatio.SelectedIndex = 0;
        }
    }

    private void CmbCropRatio_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_cropMode || cmbCropRatio.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        if (item.Tag is not ValueTuple<string, double, double> preset) return;
        if (imgPreview.Source is not BitmapSource bs) return;

        if (preset.Item2 <= 0 || preset.Item3 <= 0)
        {
            // Original / Free: full khung.
            ctrlCropBox.AspectRatio = 0;
            _cropX = 0; _cropY = 0; _cropW = 1f; _cropH = 1f;
        }
        else
        {
            double ratio = preset.Item2 / preset.Item3;
            ctrlCropBox.AspectRatio = ratio;
            var r = ZeroVision.Imaging.CropAspect.Centered(bs.PixelWidth, bs.PixelHeight, preset.Item2, preset.Item3);
            _cropX = r.X; _cropY = r.Y; _cropW = r.W; _cropH = r.H;
        }
        DrawCropOverlay();
        _developPanel?.SetCropRect(_cropX, _cropY, _cropW, _cropH);
    }

    private void BtnCrop_Click(object sender, RoutedEventArgs e) => ToggleCropMode();

    private void ToggleCropMode()
    {
        if (imgPreview.Source == null) return;
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path) || !_renderer.CanDecode(path)) return;

        _cropMode = !_cropMode;
        if (_cropMode)
        {
            _developPanel?.DisableTat();
            SetMode(LighttableMode.Single);
            ResetZoom();
            if (_developPanel != null)
            {
                var c = _developPanel.GetCropRect();
                _cropX = c.X; _cropY = c.Y; _cropW = c.W; _cropH = c.H;
            }
            cropOverlay.Visibility = Visibility.Visible;
            btnCrop.Background = ThemeManager.GetBrush("AccentBrush");
            cmbCropRatio.Visibility = Visibility.Visible;
            btnSmartCrop.Visibility = Visibility.Visible;
            // Render uncropped image then draw overlay (DrawCropOverlay called at end of RenderDevelopAsync).
            _ = RenderDevelopAsync(path);
        }
        else
        {
            cropOverlay.Visibility = Visibility.Collapsed;
            cmbCropRatio.Visibility = Visibility.Collapsed;
            btnSmartCrop.Visibility = Visibility.Collapsed;
            btnCrop.Background = ThemeManager.GetBrush("BgHoverBrush");
            // Re-render with crop applied.
            _ = RenderDevelopAsync(path);
        }
    }

    /// <summary>
    /// Smart Crop (content-aware): analyzes proxy image to find best crop window for aspect ratio
    /// (saliency + skin + center bias), then assigns to crop rectangle.
    /// </summary>
    private void BtnSmartCrop_Click(object sender, RoutedEventArgs e)
    {
        if (!_cropMode) return;
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path) || !_renderer.CanDecode(path)) return;

        // Target aspect ratio from combo (Original/Free -> 0,0 = preserve image aspect).
        double rw = 0, rh = 0;
        if (cmbCropRatio.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
            item.Tag is ValueTuple<string, double, double> preset)
        {
            rw = preset.Item2; rh = preset.Item3;
        }

        try
        {
            var r = _renderer.AnalyzeSmartCrop(path, rw, rh);
            if (r == null) return;
            _cropX = r.Value.X; _cropY = r.Value.Y; _cropW = r.Value.W; _cropH = r.Value.H;
            DrawCropOverlay();
            _developPanel?.SetCropRect(_cropX, _cropY, _cropW, _cropH);
        }
        catch (Exception ex)
        {
            ZeroVision.Shared.AppLog.Warn("CenterPreview.SmartCrop", $"{path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clone operation chain but reset crop rectangle to full frame (keep straighten angle),
    /// so preview displays uncropped image during crop adjustment (overlay matches image coordinates).
    /// </summary>
    private static System.Collections.Generic.IReadOnlyList<ZeroVision.Core.EditOperation> StripCropRect(
        System.Collections.Generic.IReadOnlyList<ZeroVision.Core.EditOperation> ops, int pointer)
    {
        var result = new System.Collections.Generic.List<ZeroVision.Core.EditOperation>(ops.Count);
        int n = Math.Min(pointer, ops.Count);
        for (int i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            if (i < n && string.Equals(op.OpType, "Crop", StringComparison.OrdinalIgnoreCase))
            {
                var p = new System.Collections.Generic.Dictionary<string, string>(op.Params)
                {
                    ["x"] = "0", ["y"] = "0", ["w"] = "1", ["h"] = "1"
                };
                result.Add(new ZeroVision.Core.EditOperation { PluginId = op.PluginId, OpType = op.OpType, Title = op.Title, Params = p });
            }
            else result.Add(op);
        }
        return result;
    }

    /// <summary>Display rectangle of image (Uniform stretch + margin) in paneSingle coordinates.</summary>
    private Rect GetDisplayedImageRect()
    {
        if (imgPreview.Source is not BitmapSource bs) return Rect.Empty;
        double margin = imgPreview.Margin.Left;
        double availW = paneSingle.ActualWidth - margin * 2;
        double availH = paneSingle.ActualHeight - margin * 2;
        if (availW <= 0 || availH <= 0) return Rect.Empty;
        double imgAspect = bs.PixelWidth / (double)bs.PixelHeight;
        double boxAspect = availW / availH;
        double dispW, dispH;
        if (imgAspect > boxAspect) { dispW = availW; dispH = availW / imgAspect; }
        else { dispH = availH; dispW = availH * imgAspect; }
        double left = margin + (availW - dispW) / 2;
        double top = margin + (availH - dispH) / 2;
        return new Rect(left, top, dispW, dispH);
    }

    private void DrawCropOverlay()
    {
        var img = GetDisplayedImageRect();
        if (img.IsEmpty || img.Width <= 0 || img.Height <= 0) return;

        ctrlCropBox.Width = img.Width;
        ctrlCropBox.Height = img.Height;
        Canvas.SetLeft(ctrlCropBox, img.Left);
        Canvas.SetTop(ctrlCropBox, img.Top);

        ctrlCropBox.CropRect = new Rect(_cropX, _cropY, _cropW, _cropH);
        ctrlCropBox.GuideMode = (CropGuideMode)_cropGuide;
    }
}
