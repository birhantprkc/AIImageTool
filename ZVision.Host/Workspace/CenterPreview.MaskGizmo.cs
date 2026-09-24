using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Wpf.Editors;
using ZVision.Imaging;

namespace ZVision.Host.Workspace;

public partial class CenterPreview
{
    private LocalMask? _gizmoMask;
    private bool _maskGizmoInitialized;

    public void BindMaskGizmo(DevelopPanel panel)
    {
        _developPanel = panel;

        if (!_maskGizmoInitialized)
        {
            _maskGizmoInitialized = true;
            ctrlMaskGizmo.GizmoChanged += (s, e) =>
            {
                if (_gizmoMask == null) return;
                if (_gizmoMask.MaskType == LinearGradientMask.Type)
                {
                    var mid = ctrlMaskGizmo.CenterPoint;
                    var end = ctrlMaskGizmo.EndPoint;
                    var start = new Point(2 * mid.X - end.X, 2 * mid.Y - end.Y);

                    _gizmoMask.MaskParams["x0"] = Format(start.X);
                    _gizmoMask.MaskParams["y0"] = Format(start.Y);
                    _gizmoMask.MaskParams["x1"] = Format(end.X);
                    _gizmoMask.MaskParams["y1"] = Format(end.Y);
                }
                else if (_gizmoMask.MaskType == RadialMask.Type)
                {
                    _gizmoMask.MaskParams["cx"] = Format(ctrlMaskGizmo.CenterPoint.X);
                    _gizmoMask.MaskParams["cy"] = Format(ctrlMaskGizmo.CenterPoint.Y);
                    _gizmoMask.MaskParams["rx"] = Format(ctrlMaskGizmo.RadiusX);
                    _gizmoMask.MaskParams["ry"] = Format(ctrlMaskGizmo.RadiusY);
                    _gizmoMask.MaskParams["feather"] = Format(ctrlMaskGizmo.Feather);
                }

                _developPanel?.NotifyMaskParamsUpdated(_gizmoMask);
            };
        }

        panel.ActiveMaskChanged += (s, mask) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (mask != null && (mask.MaskType == LinearGradientMask.Type || mask.MaskType == RadialMask.Type))
                {
                    _gizmoMask = mask;
                    maskGizmoOverlay.Visibility = Visibility.Visible;
                    RedrawMaskGizmo();
                }
                else
                {
                    _gizmoMask = null;
                    ctrlMaskGizmo.GizmoType = MaskGizmoType.None;
                    maskGizmoOverlay.Visibility = Visibility.Collapsed;
                }
            });
        };
    }

    private void RedrawMaskGizmo()
    {
        if (_gizmoMask == null)
        {
            ctrlMaskGizmo.GizmoType = MaskGizmoType.None;
            return;
        }

        var img = GetDisplayedImageRect();
        if (img.IsEmpty || img.Width <= 0 || img.Height <= 0) return;

        ctrlMaskGizmo.Width = img.Width;
        ctrlMaskGizmo.Height = img.Height;
        Canvas.SetLeft(ctrlMaskGizmo, img.Left);
        Canvas.SetTop(ctrlMaskGizmo, img.Top);

        var p = _gizmoMask.MaskParams;
        if (_gizmoMask.MaskType == LinearGradientMask.Type)
        {
            double x0 = ParseDouble(p, "x0", 0.5);
            double y0 = ParseDouble(p, "y0", 0.1);
            double x1 = ParseDouble(p, "x1", 0.5);
            double y1 = ParseDouble(p, "y1", 0.6);

            ctrlMaskGizmo.GizmoType = MaskGizmoType.LinearGradient;
            ctrlMaskGizmo.CenterPoint = new Point((x0 + x1) / 2.0, (y0 + y1) / 2.0);
            ctrlMaskGizmo.EndPoint = new Point(x1, y1);
        }
        else if (_gizmoMask.MaskType == RadialMask.Type)
        {
            double cx = ParseDouble(p, "cx", 0.5);
            double cy = ParseDouble(p, "cy", 0.5);
            double rx = ParseDouble(p, "rx", 0.25);
            double ry = ParseDouble(p, "ry", 0.25);
            double feather = ParseDouble(p, "feather", 0.4);

            ctrlMaskGizmo.GizmoType = MaskGizmoType.RadialGradient;
            ctrlMaskGizmo.CenterPoint = new Point(cx, cy);
            ctrlMaskGizmo.RadiusX = rx;
            ctrlMaskGizmo.RadiusY = ry;
            ctrlMaskGizmo.Feather = feather;
        }
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> dict, string key, double defaultVal)
    {
        return dict.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : defaultVal;
    }

    private static string Format(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
