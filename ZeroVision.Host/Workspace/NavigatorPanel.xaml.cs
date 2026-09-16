using System;
using System.Windows;
using System.Windows.Controls;

namespace ZeroVision.Host.Workspace;

public partial class NavigatorPanel : UserControl
{
    private CenterPreview? _centerPreview;

    public NavigatorPanel()
    {
        InitializeComponent();
        zoomSwitcher.Items = new[] { "FIT", "FILL", "1:1", "2:1" };
        zoomSwitcher.SelectedIndex = 0;
        zoomSwitcher.SelectedIndexChanged += ZoomSwitcher_SelectedIndexChanged;

        miniNav.ViewportMoved += (s, r) =>
        {
            if (_centerPreview != null)
            {
                double cx = r.X + r.Width / 2.0;
                double cy = r.Y + r.Height / 2.0;
                _centerPreview.PanToNormalized(Math.Clamp(cx, 0.0, 1.0), Math.Clamp(cy, 0.0, 1.0));
            }
        };
    }

    private void ZoomSwitcher_SelectedIndexChanged(object? sender, int index)
    {
        if (_centerPreview == null || index < 0) return;
        string mode = index switch
        {
            0 => "FIT",
            1 => "FILL",
            2 => "1:1",
            3 => "2:1",
            _ => "FIT"
        };
        _centerPreview.ZoomToMode(mode);
    }

    public void BindCenterPreview(CenterPreview centerPreview)
    {
        if (_centerPreview != null)
        {
            _centerPreview.ViewportChanged -= OnViewportChanged;
        }
        _centerPreview = centerPreview;
        if (_centerPreview != null)
        {
            _centerPreview.ViewportChanged += OnViewportChanged;
        }
    }

    private void OnViewportChanged(object? sender, ViewportChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            miniNav.ImageSource = e.Source;
            miniNav.ViewportRect = new Rect(e.NormX, e.NormY, e.NormW, e.NormH);

            int targetIndex = e.Zoom switch
            {
                <= 1.001 => 0,
                _ when Math.Abs(e.Zoom - 2.0) < 0.05 => 3,
                _ => 2
            };
            if (zoomSwitcher.SelectedIndex != targetIndex)
            {
                zoomSwitcher.SelectedIndexChanged -= ZoomSwitcher_SelectedIndexChanged;
                zoomSwitcher.SelectedIndex = targetIndex;
                zoomSwitcher.SelectedIndexChanged += ZoomSwitcher_SelectedIndexChanged;
            }
        });
    }
}
