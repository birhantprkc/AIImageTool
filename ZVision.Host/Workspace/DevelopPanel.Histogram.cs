using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Editors;
using ZVision.Core;
using ZVision.Imaging;

namespace ZVision.Host.Workspace;

// Visual histogram + clipping warnings in DevelopPanel using ZeroUI.Wpf.Editors.HistogramScopeControl.
public partial class DevelopPanel
{
    private Border? _histHost;
    private HistogramScopeControl? _histScopeControl;
    private TextBlock? _histClipLabel;
    private string _normalClipText = "No clipping";
    private Brush? _normalClipBrush;

    // Channel display mode: 0 = RGB overlay, 1 = Luma.
    private int _histChannelMode;
    // Scope display mode: 0 = histogram, 1 = waveform/parade.
    private int _scopeMode;
    private ToggleButton? _histBtnRgb;
    private ToggleButton? _histBtnLuma;
    private ToggleButton? _histBtnWave;

    private HistogramData? _lastHist;
    private WaveformData? _lastWave;

    /// <summary>Build histogram widget (called first in BuildUI, pinned at top).</summary>
    private FrameworkElement BuildHistogram()
    {
        var outer = new StackPanel { Margin = new Thickness(2, 2, 2, 6) };

        // RGB / Luma channel buttons + Waveform scope.
        var toggleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 2)
        };

        _histBtnRgb = new ToggleButton { Content = "RGB", FontSize = 10, Padding = new Thickness(6, 1, 6, 1), IsChecked = true, Margin = new Thickness(0, 0, 4, 0) };
        _histBtnLuma = new ToggleButton { Content = "Luma", FontSize = 10, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 8, 0) };
        _histBtnWave = new ToggleButton { Content = "Wave", FontSize = 10, Padding = new Thickness(6, 1, 6, 1), ToolTip = "Toggle Waveform / RGB Parade (column-wise distribution)" };

        _histBtnRgb.Click += (_, _) => SetHistChannelMode(0);
        _histBtnLuma.Click += (_, _) => SetHistChannelMode(1);
        _histBtnWave.Click += (_, _) => SetScopeMode(_histBtnWave.IsChecked == true ? 1 : 0);

        toggleRow.Children.Add(_histBtnRgb);
        toggleRow.Children.Add(_histBtnLuma);
        toggleRow.Children.Add(_histBtnWave);
        outer.Children.Add(toggleRow);

        _histScopeControl = new HistogramScopeControl
        {
            Height = 110,
            ToolTip = "Drag horizontally on tonal zones: Blacks · Shadows · Exposure · Highlights · Whites"
        };
        _histScopeControl.ZoneDragged += HistScope_ZoneDragged;
        _histScopeControl.ZoneReset += HistScope_ZoneReset;

        _histHost = new Border
        {
            BorderBrush = ThemeManager.GetBrush("BorderBrush_"),
            BorderThickness = new Thickness(1),
            Child = _histScopeControl
        };
        outer.Children.Add(_histHost);

        _histClipLabel = new TextBlock
        {
            Foreground = ThemeManager.GetBrush("TextDimBrush"),
            FontSize = 10,
            Margin = new Thickness(2, 2, 0, 0)
        };
        outer.Children.Add(_histClipLabel);

        return outer;
    }

    private void SetHistChannelMode(int mode)
    {
        _histChannelMode = mode;
        if (_histBtnRgb != null) _histBtnRgb.IsChecked = mode == 0;
        if (_histBtnLuma != null) _histBtnLuma.IsChecked = mode == 1;
        DrawScope();
    }

    private void SetScopeMode(int mode)
    {
        _scopeMode = mode;
        if (_histBtnWave != null) _histBtnWave.IsChecked = mode == 1;
        RefreshHistogram(); // reload scope data (waveform/histogram)
    }

    /// <summary>Recalculate scope for image + current ops off-UI, then render.</summary>
    private void RefreshHistogram()
    {
        if (_renderer == null || _history == null || string.IsNullOrEmpty(_currentPath)) return;
        var path = _currentPath;
        var ops = _history.GetStack(path);
        int ptr = _history.GetPointer(path);
        bool wave = _scopeMode == 1;

        System.Threading.Tasks.Task.Run(() =>
        {
            if (wave)
            {
                var wf = _renderer.ComputeWaveform(path, ops, ptr, 256);
                if (wf == null) return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (!string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase)) return;
                    _lastWave = wf;
                    DrawScope();
                });
            }
            else
            {
                var hist = _renderer.ComputeHistogram(path, ops, ptr);
                if (hist == null) return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (!string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase)) return;
                    _lastHist = hist;
                    DrawScope();
                });
            }
        });
    }

    private void DrawScope()
    {
        if (_scopeMode == 1) DrawWaveform();
        else DrawHistogram();
    }

    private void DrawHistogram()
    {
        if (_histScopeControl == null || _lastHist == null) return;
        var hist = _lastHist;

        _histScopeControl.ChannelMode = _histChannelMode == 1 ? HistogramChannelMode.Luma : HistogramChannelMode.Rgb;
        _histScopeControl.SetChannels(hist.R, hist.G, hist.B, hist.Luma);
        _histScopeControl.ShadowClipPercent = hist.ShadowClipWarning ? hist.ShadowClipPercent : 0.0;
        _histScopeControl.HighlightClipPercent = hist.HighlightClipWarning ? hist.HighlightClipPercent : 0.0;

        if (_histHost != null && _histHost.Child != _histScopeControl)
        {
            _histHost.Child = _histScopeControl;
        }

        UpdateClipLabel();
    }

    private void UpdateClipLabel()
    {
        if (_histClipLabel == null || _lastHist == null) return;
        var hist = _lastHist;
        var parts = new List<string>();
        if (hist.ShadowClipWarning) parts.Add($"▼ Shadow {hist.ShadowClipPercent:0.0}%");
        if (hist.HighlightClipWarning) parts.Add($"▲ Highlight {hist.HighlightClipPercent:0.0}%");
        _normalClipText = parts.Count > 0 ? string.Join("   ", parts) : "No clipping";
        _normalClipBrush = parts.Count > 0 ? Brushes.Orange : ThemeManager.GetBrush("TextDimBrush");
        _histClipLabel.Text = _normalClipText;
        _histClipLabel.Foreground = _normalClipBrush;
    }

    private void HistScope_ZoneDragged(object? sender, HistogramZoneDragEventArgs e)
    {
        if (_loading || string.IsNullOrEmpty(_currentPath)) return;
        string key = e.ZoneIndex switch
        {
            0 => "blacks",
            1 => "shadows",
            2 => "exposure",
            3 => "highlights",
            _ => "whites"
        };

        double gain = key == "exposure" ? 4.0 : 1.6;
        double cur = GetVal(key);
        double next = cur + e.NormalizedDelta * gain;
        next = key == "exposure" ? Math.Clamp(next, -5, 5) : Math.Clamp(next, -1, 1);
        SetVal(key, next);

        if (_histClipLabel != null)
        {
            double val = GetVal(key);
            string valStr = key == "exposure" ? $"{val:+0.00;-0.00;0.00} EV" : $"{val * 100:+0;-0;0}";
            _histClipLabel.Text = $"{e.ZoneName}: {valStr}";
            _histClipLabel.Foreground = ThemeManager.GetBrush("AccentBrush");
        }
    }

    private void HistScope_ZoneReset(object? sender, int zoneIndex)
    {
        if (_loading || string.IsNullOrEmpty(_currentPath)) return;
        string key = zoneIndex switch
        {
            0 => "blacks",
            1 => "shadows",
            2 => "exposure",
            3 => "highlights",
            _ => "whites"
        };
        SetVal(key, 0.0);
        UpdateClipLabel();
    }

    /// <summary>Vẽ waveform/RGB-parade bằng WriteableBitmap (additive theo cột). RGB mode = 3 kênh chồng màu.</summary>
    private void DrawWaveform()
    {
        if (_histHost == null || _lastWave == null) return;
        double cw = _histHost.ActualWidth > 0 ? _histHost.ActualWidth : 260;
        double ch = _histHost.ActualHeight > 0 ? _histHost.ActualHeight : 110;

        var wf = _lastWave;
        int cols = wf.Columns;
        int bw = cols, bh = 256;
        var bmp = new WriteableBitmap(bw, bh, 96, 96, PixelFormats.Bgra32, null);
        var buf = new byte[bw * bh * 4];

        float norm = wf.MaxCount > 0 ? 1f / MathF.Log(1 + wf.MaxCount) : 1f;
        bool luma = _histChannelMode == 1;

        for (int c = 0; c < cols; c++)
        {
            for (int v = 0; v < 256; v++)
            {
                int yRow = (255 - v) * bw;
                int o = (yRow + c) * 4;
                if (luma)
                {
                    byte i = Intensity(wf.Luma[c, v], norm);
                    if (i == 0) continue;
                    buf[o] = i; buf[o + 1] = i; buf[o + 2] = i; buf[o + 3] = 255;
                }
                else
                {
                    byte rr = Intensity(wf.R[c, v], norm);
                    byte gg = Intensity(wf.G[c, v], norm);
                    byte bb = Intensity(wf.B[c, v], norm);
                    if ((rr | gg | bb) == 0) continue;
                    buf[o] = bb; buf[o + 1] = gg; buf[o + 2] = rr; buf[o + 3] = 255;
                }
            }
        }
        bmp.WritePixels(new Int32Rect(0, 0, bw, bh), buf, bw * 4, 0);

        var img = new Image
        {
            Source = bmp,
            Stretch = Stretch.Fill,
            Width = cw,
            Height = ch
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Linear);
        _histHost.Child = img;

        if (_histClipLabel != null)
        {
            _histClipLabel.Text = luma ? "Waveform (Luma)" : "RGB Parade";
            _histClipLabel.Foreground = ThemeManager.GetBrush("TextDimBrush");
        }
    }

    private static byte Intensity(int count, float norm)
    {
        if (count <= 0) return 0;
        float t = MathF.Log(1 + count) * norm;
        int v = (int)(40 + t * 215);
        if (v > 255) v = 255;
        return (byte)v;
    }
}
