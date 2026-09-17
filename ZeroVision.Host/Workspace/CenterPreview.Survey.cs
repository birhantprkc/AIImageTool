using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

public partial class CenterPreview
{
    private double _surveyZoom = 1.0;
    private double _surveyPanX;
    private double _surveyPanY;
    private bool _isDraggingSurveyPan;
    private Point _surveyPanStartMouse;
    private double _surveyPanStartX;
    private double _surveyPanStartY;
    private bool _surveyPeakingActive;
    private bool _surveyEventsInitialized;
    private readonly List<TranslateTransform> _surveyPanTransforms = new();
    private readonly List<ScaleTransform> _surveyScaleTransforms = new();

    private void EnsureSurveyEvents()
    {
        if (_surveyEventsInitialized) return;
        _surveyEventsInitialized = true;

        paneCull.ClipToBounds = true;
        paneCull.MouseWheel += (s, e) =>
        {
            if (_mode != LighttableMode.Cull) return;
            double factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            StepSurveyZoom(factor);
            e.Handled = true;
        };

        paneCull.MouseRightButtonDown += (s, e) =>
        {
            if (_mode != LighttableMode.Cull || _surveyZoom <= 1.0) return;
            _isDraggingSurveyPan = true;
            _surveyPanStartMouse = e.GetPosition(paneCull);
            _surveyPanStartX = _surveyPanX;
            _surveyPanStartY = _surveyPanY;
            paneCull.Cursor = Cursors.Hand;
            paneCull.CaptureMouse();
            e.Handled = true;
        };

        paneCull.MouseMove += (s, e) =>
        {
            if (!_isDraggingSurveyPan || _mode != LighttableMode.Cull) return;
            var cur = e.GetPosition(paneCull);
            _surveyPanX = _surveyPanStartX + (cur.X - _surveyPanStartMouse.X);
            _surveyPanY = _surveyPanStartY + (cur.Y - _surveyPanStartMouse.Y);
            ApplySurveyTransforms();
            e.Handled = true;
        };

        paneCull.MouseRightButtonUp += (s, e) =>
        {
            if (_isDraggingSurveyPan)
            {
                _isDraggingSurveyPan = false;
                paneCull.ReleaseMouseCapture();
                paneCull.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        };
    }

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

    /// <summary>Toggles High-Frequency Focus Peaking across all visible survey candidates.</summary>
    public void ToggleSurveyPeaking()
    {
        _surveyPeakingActive = !_surveyPeakingActive;
        RebuildCullView();
    }

    /// <summary>Toggles synchronous zoom between Fit (1.0) and 2.0 on all candidate viewports.</summary>
    public void ToggleSurveyZoom()
    {
        if (_surveyZoom > 1.05)
        {
            _surveyZoom = 1.0;
            _surveyPanX = 0;
            _surveyPanY = 0;
        }
        else
        {
            _surveyZoom = 2.0;
        }
        ApplySurveyTransforms();
    }

    /// <summary>Steps synchronous zoom on all candidate viewports by the given factor.</summary>
    public void StepSurveyZoom(double factor)
    {
        _surveyZoom = Math.Clamp(_surveyZoom * factor, 1.0, 5.0);
        if (_surveyZoom <= 1.02)
        {
            _surveyZoom = 1.0;
            _surveyPanX = 0;
            _surveyPanY = 0;
        }
        ApplySurveyTransforms();
    }

    private void ApplySurveyTransforms()
    {
        foreach (var st in _surveyScaleTransforms)
        {
            st.ScaleX = _surveyZoom;
            st.ScaleY = _surveyZoom;
        }
        foreach (var tt in _surveyPanTransforms)
        {
            tt.X = _surveyPanX;
            tt.Y = _surveyPanY;
        }
    }

    /// <summary>
    /// Navigates the active photo index strictly within the Survey candidate set.
    /// </summary>
    public void NavigateSurveyActive(int delta)
    {
        if (_workspace == null) return;
        var sel = _workspace.Selection.ToList();
        if (sel.Count == 0 && _workspace.ActiveImage != null) sel.Add(_workspace.ActiveImage);
        if (sel.Count <= 1) return;

        int idx = sel.FindIndex(p => string.Equals(p, _workspace.ActiveImage, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;
        int next = (idx + delta + sel.Count) % sel.Count;
        _workspace.SetActiveImage(sel[next]);
        RebuildCullView();
    }

    /// <summary>
    /// Navigates rows of active photo in the Survey grid.
    /// </summary>
    public void NavigateSurveyRow(int deltaRow)
    {
        if (_workspace == null || paneCull.Columns <= 0) return;
        var sel = _workspace.Selection.ToList();
        if (sel.Count == 0 && _workspace.ActiveImage != null) sel.Add(_workspace.ActiveImage);
        if (sel.Count <= 1) return;

        int cols = paneCull.Columns;
        NavigateSurveyActive(deltaRow * cols);
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
        EnsureSurveyEvents();
        paneCull.Children.Clear();
        _surveyScaleTransforms.Clear();
        _surveyPanTransforms.Clear();

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
            bool isActive = string.Equals(p, _workspace?.ActiveImage, StringComparison.OrdinalIgnoreCase);

            var cardGrid = new Grid();

            // Viewport container for synchronized zoom & pan
            var viewportBorder = new Border
            {
                ClipToBounds = true,
                Margin = new Thickness(6, 30, 6, 26)
            };

            var imageHostGrid = new Grid();

            // Transform group for locked sync zoom and pan
            var scaleTrans = new ScaleTransform(_surveyZoom, _surveyZoom);
            var panTrans = new TranslateTransform(_surveyPanX, _surveyPanY);
            var transGroup = new TransformGroup();
            transGroup.Children.Add(scaleTrans);
            transGroup.Children.Add(panTrans);

            _surveyScaleTransforms.Add(scaleTrans);
            _surveyPanTransforms.Add(panTrans);

            imageHostGrid.RenderTransform = transGroup;
            imageHostGrid.RenderTransformOrigin = new Point(0.5, 0.5);

            // 1. Image preview
            var img = new Image
            {
                Stretch = Stretch.Uniform
            };

            BitmapSource? loadedBitmap = null;
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
                loadedBitmap = bmp;
            }
            catch { }
            imageHostGrid.Children.Add(img);

            // 1b. Focus Peaking Overlay if active
            if (_surveyPeakingActive && loadedBitmap != null)
            {
                try
                {
                    var peakMask = BuildPeakMask(loadedBitmap);
                    var peakImg = new Image
                    {
                        Source = peakMask,
                        Stretch = Stretch.Uniform,
                        IsHitTestVisible = false
                    };
                    imageHostGrid.Children.Add(peakImg);
                }
                catch { }
            }

            viewportBorder.Child = imageHostGrid;
            cardGrid.Children.Add(viewportBorder);

            // 2. Top Header Bar (Number badge + Active indicator + Focus Peaking status + Dismiss button)
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
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 6, 0)
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

            if (_surveyPeakingActive)
            {
                var peakBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 30, 140, 30)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(5, 2, 5, 2)
                };
                peakBadge.Child = new TextBlock
                {
                    Text = "PEAK",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                leftBadges.Children.Add(peakBadge);
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

            // 3. Bottom Info Bar (File name, Rating, Pick/Reject flag, Color Label)
            var bottomBar = new DockPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 8, 6)
            };

            var metaRight = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(metaRight, Dock.Right);

            // Fetch live metadata
            var meta = _meta?.Get(p);
            if (meta != null)
            {
                // Rating stars
                if (meta.Rating > 0)
                {
                    var txtRating = new TextBlock
                    {
                        Text = new string('★', meta.Rating),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 40)),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    metaRight.Children.Add(txtRating);
                }

                // Pick / Reject Flag
                if (meta.Pick == PickFlag.Pick)
                {
                    var pickBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(180, 40, 160, 60)),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    pickBadge.Child = new TextBlock { Text = "PICK", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                    metaRight.Children.Add(pickBadge);
                }
                else if (meta.Pick == PickFlag.Reject)
                {
                    var rejectBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(180, 200, 40, 40)),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    rejectBadge.Child = new TextBlock { Text = "REJECT", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                    metaRight.Children.Add(rejectBadge);
                }

                // Color Label
                if (meta.Label != ColorLabel.None)
                {
                    var labelColor = meta.Label switch
                    {
                        ColorLabel.Red => Color.FromRgb(231, 76, 60),
                        ColorLabel.Yellow => Color.FromRgb(241, 196, 15),
                        ColorLabel.Green => Color.FromRgb(46, 204, 113),
                        ColorLabel.Blue => Color.FromRgb(52, 152, 219),
                        ColorLabel.Purple => Color.FromRgb(155, 89, 182),
                        _ => Colors.Transparent
                    };
                    var labelSwatch = new Border
                    {
                        Width = 10,
                        Height = 10,
                        CornerRadius = new CornerRadius(5),
                        Background = new SolidColorBrush(labelColor),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    metaRight.Children.Add(labelSwatch);
                }
            }

            bottomBar.Children.Add(metaRight);

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

            if (_workspace != null && _meta != null && _history != null && _clipboard != null)
            {
                border.ContextMenu = ImageContextMenu.Build(capturedPath, _workspace, _meta, _history, _clipboard, SetReferenceImage);
            }

            // Click card -> set active image
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (_workspace != null && !string.Equals(_workspace.ActiveImage, capturedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _workspace.SetActiveImage(capturedPath);
                    RebuildCullView();
                }
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                if (_workspace != null && !string.Equals(_workspace.ActiveImage, capturedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _workspace.SetActiveImage(capturedPath);
                }
            };

            paneCull.Children.Add(border);
        }
    }
}
