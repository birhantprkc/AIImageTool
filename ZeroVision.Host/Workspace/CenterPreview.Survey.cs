using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZeroVision.Host.Workspace;

public partial class CenterPreview
{
    /// <summary>Toggles between Survey (Cull) mode and Single Loupe mode.</summary>
    public void ToggleSurveyMode()
    {
        if (_mode == LighttableMode.Cull)
        {
            SetMode(LighttableMode.Single);
        }
        else
        {
            SetMode(LighttableMode.Cull);
        }
    }

    /// <summary>
    /// Dismisses the currently active photo from the Survey selection.
    /// Returns true if an item was dismissed.
    /// </summary>
    public bool DismissActiveFromSurvey()
    {
        if (_workspace == null || _mode != LighttableMode.Cull) return false;
        var active = _workspace.ActiveImage;
        if (string.IsNullOrEmpty(active)) return false;

        var sel = _workspace.Selection.ToList();
        if (sel.Contains(active, StringComparer.OrdinalIgnoreCase))
        {
            _workspace.RemoveFromSelection(active);
            var remaining = _workspace.Selection.ToList();
            if (remaining.Count > 0)
            {
                _workspace.SetActiveImage(remaining[0]);
            }
            RebuildCullView();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Rebuilds the responsive multi-photo survey comparison grid.
    /// Arranges candidate photos in a balanced grid with dismiss buttons and active indicators.
    /// </summary>
    public void RebuildCullView()
    {
        paneCull.Children.Clear();
        if (_workspace == null) return;

        // Collect photos for survey comparison
        var sel = _workspace.Selection.ToList();
        if (sel.Count == 0 && _workspace.ActiveImage != null)
        {
            sel.Add(_workspace.ActiveImage);
        }

        if (sel.Count == 0)
        {
            paneCull.Columns = 1;
            paneCull.Rows = 1;
            var emptyNotice = new TextBlock
            {
                Text = "No photos selected for Survey.\nSelect 2 or more photos in the filmstrip and press N to compare and cull candidates.",
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 22
            };
            emptyNotice.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            paneCull.Children.Add(emptyNotice);
            return;
        }

        // Calculate balanced columns and rows
        int count = sel.Count;
        int cols = 1, rows = 1;
        if (count <= 1) { cols = 1; rows = 1; }
        else if (count == 2) { cols = 2; rows = 1; }
        else if (count <= 4) { cols = 2; rows = (count + 1) / 2; }
        else if (count <= 6) { cols = 3; rows = 2; }
        else if (count <= 9) { cols = 3; rows = 3; }
        else if (count <= 12) { cols = 4; rows = 3; }
        else { cols = 4; rows = (int)Math.Ceiling(count / 4.0); }

        paneCull.Columns = cols;
        paneCull.Rows = rows;

        for (int i = 0; i < sel.Count; i++)
        {
            var p = sel[i];
            bool isActive = string.Equals(p, _workspace.ActiveImage, StringComparison.OrdinalIgnoreCase);

            var cardGrid = new Grid();

            // 1. Image preview
            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(6, 30, 6, 26)
            };

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(p);
                bmp.DecodePixelWidth = 1400;
                bmp.EndInit();
                bmp.Freeze();
                img.Source = bmp;
            }
            catch { }
            cardGrid.Children.Add(img);

            // 2. Top Header Bar (Number badge + Active indicator + Dismiss button)
            var topBar = new DockPanel
            {
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 8, 8, 0)
            };

            var leftBadges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(leftBadges, Dock.Left);

            // Index badge (#1, #2...)
            var indexBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 6, 0)
            };
            indexBadge.Child = new TextBlock
            {
                Text = $"#{i + 1}",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            leftBadges.Children.Add(indexBadge);

            // Active badge
            if (isActive)
            {
                var activeBadge = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                activeBadge.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                activeBadge.Child = new TextBlock
                {
                    Text = "ACTIVE",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                leftBadges.Children.Add(activeBadge);
            }
            topBar.Children.Add(leftBadges);

            // Dismiss Button (✖)
            var btnDismiss = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(160, 25, 25, 25)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Dismiss from survey selection (remove candidate)"
            };
            DockPanel.SetDock(btnDismiss, Dock.Right);

            string capturedPath = p;
            btnDismiss.Click += (s, e) =>
            {
                e.Handled = true;
                _workspace.RemoveFromSelection(capturedPath);
                var rem = _workspace.Selection.ToList();
                if (isActive && rem.Count > 0)
                {
                    _workspace.SetActiveImage(rem[0]);
                }
                RebuildCullView();
            };
            topBar.Children.Add(btnDismiss);

            cardGrid.Children.Add(topBar);

            // 3. Bottom Info Bar (File name and rating)
            var bottomBar = new DockPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 8, 6)
            };

            var txtName = new TextBlock
            {
                Text = Path.GetFileName(p),
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtName.SetResourceReference(TextBlock.ForegroundProperty, isActive ? "TextPrimaryBrush" : "TextSecondaryBrush");
            bottomBar.Children.Add(txtName);

            cardGrid.Children.Add(bottomBar);

            // Card Outer Border
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(4),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Child = cardGrid
            };
            border.SetResourceReference(Border.BackgroundProperty, "BgPanelBrush");

            if (isActive)
            {
                border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                border.BorderThickness = new Thickness(2);
            }
            else
            {
                border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush_");
                border.BorderThickness = new Thickness(1);
            }

            // Click card -> set active image
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (!string.Equals(_workspace.ActiveImage, capturedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _workspace.SetActiveImage(capturedPath);
                    RebuildCullView();
                }
            };

            paneCull.Children.Add(border);
        }
    }
}
