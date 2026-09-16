using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroVision.Shared;

namespace ZeroVision.Host;

public partial class CopySettingsDialog : Window
{
    private static readonly HashSet<string> DefaultKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "basic", "curve", "tonemap", "input",
        "wb", "colormix", "colorgrade", "bw", "lut",
        "detail", "effects"
    };

    private static HashSet<string>? s_lastSelectedKeys;

    private readonly Dictionary<string, CheckBox> _keyToCheckBox;

    public HashSet<string> SelectedKeys { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public CopySettingsDialog()
    {
        InitializeComponent();

        _keyToCheckBox = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase)
        {
            ["basic"] = chkBasic,
            ["curve"] = chkCurve,
            ["tonemap"] = chkTonemap,
            ["input"] = chkInput,
            ["wb"] = chkWb,
            ["colormix"] = chkColormix,
            ["colorgrade"] = chkColorgrade,
            ["bw"] = chkBw,
            ["lut"] = chkLut,
            ["invert"] = chkInvert,
            ["detail"] = chkDetail,
            ["effects"] = chkEffects,
            ["ai"] = chkAi,
            ["local"] = chkLocal,
            ["healing"] = chkHealing,
            ["geometry"] = chkGeometry
        };

        var initial = s_lastSelectedKeys ?? DefaultKeys;
        foreach (var (k, cb) in _keyToCheckBox)
        {
            cb.IsChecked = initial.Contains(k);
        }
    }

    private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _keyToCheckBox.Values)
            cb.IsChecked = true;
    }

    private void BtnCheckNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _keyToCheckBox.Values)
            cb.IsChecked = false;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        SelectedKeys.Clear();
        foreach (var (k, cb) in _keyToCheckBox)
        {
            if (cb.IsChecked == true)
                SelectedKeys.Add(k);
        }

        s_lastSelectedKeys = new HashSet<string>(SelectedKeys, StringComparer.OrdinalIgnoreCase);
        DialogResult = true;
        Close();
    }
}
