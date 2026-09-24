using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZVision.Core;
using ZVision.Imaging;

namespace ZVision.Host.Workspace;

// Healing/Clone brush UI (#6). Spots stored in DevelopPanel, round-tripped via history as HealingOp.
public partial class DevelopPanel
{
    private enum ActiveBrushMode
    {
        Heal,
        Clone,
        AiInpaint
    }

    private readonly List<HealingOp.Spot> _healSpots = new();
    private HealingOp.HealMode _healMode = HealingOp.HealMode.Heal;
    private ActiveBrushMode _brushMode = ActiveBrushMode.Heal;
    private float _healRadius = 0.03f;
    private CheckBox? _chkHealActive;
    private ComboBox? _cmbHealMode;
    private TextBlock? _healInfo;

    /// <summary>Fires true when Heal/Inpaint active (CenterPreview enables spot click), false when disabled.</summary>
    public event EventHandler<bool>? HealingModeChanged;

    /// <summary>Current heal radius (normalized) — CenterPreview reads to draw + auto-source.</summary>
    public float HealRadius => _healRadius;

    private Expander? _healExpander;

    /// <summary>Expand + scroll to Healing section and activate healing brush (Q shortcut).</summary>
    public void FocusHealing()
    {
        if (_healExpander != null)
        {
            _healExpander.IsExpanded = true;
            _healExpander.BringIntoView();
        }
        if (_chkHealActive != null)
        {
            _chkHealActive.IsChecked = true;
        }
    }

    private void BuildHealingUI(StackPanel host)
    {
        _healExpander = host.Parent as Expander;
        _chkHealActive = new CheckBox
        {
            Content = "Enable Healing / Inpaint (click photo to erase)", FontSize = 11,
            Margin = new Thickness(0, 2, 0, 4)
        };
        _chkHealActive.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        _chkHealActive.Checked += (_, _) => HealingModeChanged?.Invoke(this, true);
        _chkHealActive.Unchecked += (_, _) => HealingModeChanged?.Invoke(this, false);
        host.Children.Add(_chkHealActive);

        var modeRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        modeRow.Children.Add(new TextBlock { Text = "Mode", Foreground = ThemeManager.GetBrush("TextDimBrush"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        var cmbMode = new ComboBox { Height = 22, Margin = new Thickness(6, 0, 0, 0) };
        cmbMode.Items.Add(new ComboBoxItem { Content = "Heal" });
        cmbMode.Items.Add(new ComboBoxItem { Content = "Clone" });
        cmbMode.Items.Add(new ComboBoxItem { Content = "✨ AI Inpaint (PDE Diffusion)" });
        _cmbHealMode = cmbMode;
        cmbMode.SelectedIndex = 0;
        cmbMode.SelectionChanged += (_, _) =>
        {
            _brushMode = cmbMode.SelectedIndex switch
            {
                1 => ActiveBrushMode.Clone,
                2 => ActiveBrushMode.AiInpaint,
                _ => ActiveBrushMode.Heal
            };
            _healMode = _brushMode == ActiveBrushMode.Clone ? HealingOp.HealMode.Clone : HealingOp.HealMode.Heal;
            UpdateHealInfo();
            if (!_loading && _healSpots.Count > 0) Commit();
        };
        modeRow.Children.Add(cmbMode);
        host.Children.Add(modeRow);

        var row = MakeSliderRow("Brush Size", 0.005, 0.12, _healRadius, "0.000", out var slider);
        slider.ValueChanged += (_, e) => { _healRadius = (float)e.NewValue; };
        host.Children.Add(row);

        var btnUndo = new Button { Content = "↶ Undo Last Spot", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 2, 0, 2) };
        btnUndo.Click += (_, _) =>
        {
            if (_healSpots.Count > 0) { _healSpots.RemoveAt(_healSpots.Count - 1); UpdateHealInfo(); Commit(); }
        };
        host.Children.Add(btnUndo);

        _healInfo = new TextBlock { Foreground = ThemeManager.GetBrush("TextDimBrush"), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
        host.Children.Add(_healInfo);
        UpdateHealInfo();
    }

    private void UpdateHealInfo()
    {
        if (_healInfo != null)
        {
            var modeName = _brushMode switch
            {
                ActiveBrushMode.Clone => "Clone",
                ActiveBrushMode.AiInpaint => "AI Inpaint",
                _ => "Heal"
            };
            _healInfo.Text = $"{_healSpots.Count} spot(s) [{modeName}]";
        }
    }

    /// <summary>Called by CenterPreview when user clicks a point. Auto-picks clean neighbor source.</summary>
    public void AddHealSpot(float tx, float ty)
    {
        if (_currentPath == null || _history == null) return;
        // nguồn auto: dịch ngang 1 khoảng = 2.5×bán kính (về phía có chỗ trống trong khung).
        float dxn = _healRadius * 2.5f;
        float sx = tx - dxn >= _healRadius ? tx - dxn : tx + dxn;
        float sy = ty;
        sx = Math.Clamp(sx, 0f, 1f);
        _healSpots.Add(new HealingOp.Spot(tx, ty, sx, sy, _healRadius));
        UpdateHealInfo();
        Commit();
    }

    /// <summary>Sinh HealingOp hoặc AiInpaintOp từ spots (gọi trong BuildOps). Rỗng nếu chưa có chấm.</summary>
    private void AppendHealingOp(List<EditOperation> ops)
    {
        if (_healSpots.Count == 0) return;

        if (_brushMode == ActiveBrushMode.AiInpaint)
        {
            var op = new AiInpaintOp
            {
                Algorithm = InpaintAlgorithm.FastTeleaDiffusion,
                Strength = 1.0f,
                Iterations = 8
            };
            foreach (var s in _healSpots)
            {
                op.Regions.Add(new AiInpaintOp.InpaintRegion(s.Tx, s.Ty, s.Radius));
            }
            ops.Add(Op(AiInpaintOp.Type, "AI Inpaint", op.ToParams()));
        }
        else
        {
            var op = new HealingOp { Mode = _healMode };
            op.Spots.AddRange(_healSpots);
            ops.Add(Op(HealingOp.Type, "Healing", op.ToParams()));
        }
    }

    /// <summary>Nạp lại spots từ history (gọi trong LoadFor).</summary>
    private void LoadHealing(string path)
    {
        _healSpots.Clear();
        var pInpaint = FindOp(path, AiInpaintOp.Type);
        if (pInpaint != null)
        {
            var inpaintOp = (AiInpaintOp)AiInpaintOp.Create(pInpaint);
            foreach (var r in inpaintOp.Regions)
            {
                _healSpots.Add(new HealingOp.Spot(r.NormalizedX, r.NormalizedY, r.NormalizedX, r.NormalizedY, r.Radius));
            }
            _brushMode = ActiveBrushMode.AiInpaint;
            if (_cmbHealMode != null) _cmbHealMode.SelectedIndex = 2;
        }
        else
        {
            var p = FindOp(path, HealingOp.Type);
            if (p != null)
            {
                var op = HealingOp.FromParams(p);
                _healSpots.AddRange(op.Spots);
                _healMode = op.Mode;
                _brushMode = op.Mode == HealingOp.HealMode.Clone ? ActiveBrushMode.Clone : ActiveBrushMode.Heal;
                if (_cmbHealMode != null) _cmbHealMode.SelectedIndex = _brushMode == ActiveBrushMode.Clone ? 1 : 0;
            }
        }
        UpdateHealInfo();
    }

    private void ClearHealing()
    {
        _healSpots.Clear();
        if (_chkHealActive != null) _chkHealActive.IsChecked = false;
        UpdateHealInfo();
    }
}
