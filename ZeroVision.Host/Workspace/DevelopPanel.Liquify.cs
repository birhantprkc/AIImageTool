using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroVision.Core;
using ZeroVision.Imaging;

namespace ZeroVision.Host.Workspace;

// Liquify/Warp UI (D3.5). Handles stored in DevelopPanel, round-tripped via history as LiquifyOp.
public partial class DevelopPanel
{
    private readonly List<LiquifyOp.Warp> _warps = new();
    private float _liquifyRadius = 0.15f;
    private CheckBox? _chkLiquifyActive;
    private TextBlock? _liquifyInfo;

    /// <summary>Fires true when Liquify enabled (CenterPreview allows handle drag), false when disabled.</summary>
    public event EventHandler<bool>? LiquifyActivated;

    /// <summary>Fires when warp list changes (CenterPreview redraws overlay).</summary>
    public event EventHandler? LiquifyChanged;

    /// <summary>CenterPreview reads current warp list to render overlay.</summary>
    public IReadOnlyList<LiquifyOp.Warp> GetWarps() => _warps;

    private void BuildLiquifyUI(StackPanel host)
    {
        _chkLiquifyActive = new CheckBox
        {
            Content = "Enable Liquify (drag on photo to warp)", FontSize = 11,
            Margin = new Thickness(0, 2, 0, 4)
        };
        _chkLiquifyActive.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        _chkLiquifyActive.Checked += (_, _) => LiquifyActivated?.Invoke(this, true);
        _chkLiquifyActive.Unchecked += (_, _) => LiquifyActivated?.Invoke(this, false);
        host.Children.Add(_chkLiquifyActive);

        var row = MakeSliderRow("Brush Size", 0.03, 0.5, _liquifyRadius, "0.00", out var slider);
        slider.ValueChanged += (_, e) => { _liquifyRadius = (float)e.NewValue; };
        host.Children.Add(row);

        var btnUndo = new Button { Content = "↶ Undo Last Handle", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 2, 0, 2) };
        btnUndo.Click += (_, _) =>
        {
            if (_warps.Count > 0)
            {
                _warps.RemoveAt(_warps.Count - 1);
                UpdateLiquifyInfo();
                LiquifyChanged?.Invoke(this, EventArgs.Empty);
                Commit();
            }
        };
        host.Children.Add(btnUndo);

        var btnClear = new Button { Content = "✕ Clear All Handles", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 0, 2) };
        btnClear.Click += (_, _) =>
        {
            if (_warps.Count > 0)
            {
                _warps.Clear();
                UpdateLiquifyInfo();
                LiquifyChanged?.Invoke(this, EventArgs.Empty);
                Commit();
            }
        };
        host.Children.Add(btnClear);

        _liquifyInfo = new TextBlock { Foreground = ThemeManager.GetBrush("TextDimBrush"), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
        host.Children.Add(_liquifyInfo);
        UpdateLiquifyInfo();
    }

    private void UpdateLiquifyInfo()
    {
        if (_liquifyInfo != null) _liquifyInfo.Text = $"{_warps.Count} handle";
    }

    /// <summary>Called by CenterPreview when a warp drag completes.</summary>
    public void AddWarp(float cx, float cy, float dx, float dy)
    {
        if (_currentPath == null || _history == null) return;
        _warps.Add(new LiquifyOp.Warp { Cx = cx, Cy = cy, Dx = dx, Dy = dy, Radius = _liquifyRadius });
        UpdateLiquifyInfo();
        LiquifyChanged?.Invoke(this, EventArgs.Empty);
        Commit();
    }

    /// <summary>Generates LiquifyOp from handles (called in BuildOps).</summary>
    private void AppendLiquifyOp(List<EditOperation> ops)
    {
        if (_warps.Count == 0) return;
        var op = new LiquifyOp();
        op.Warps.AddRange(_warps);
        if (op.IsIdentity) return;
        ops.Add(Op(LiquifyOp.Type, "Liquify", op.ToParams()));
    }

    /// <summary>Loads handles from history (called in LoadFor).</summary>
    private void LoadLiquify(string path)
    {
        _warps.Clear();
        var p = FindOp(path, LiquifyOp.Type);
        if (p != null)
            _warps.AddRange(LiquifyOp.FromParams(p).Warps);
        UpdateLiquifyInfo();
        LiquifyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearLiquify()
    {
        _warps.Clear();
        if (_chkLiquifyActive != null) _chkLiquifyActive.IsChecked = false;
        UpdateLiquifyInfo();
        LiquifyChanged?.Invoke(this, EventArgs.Empty);
    }
}
