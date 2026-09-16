using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

public enum LighttableMode { Single, Grid, Cull, Full, Reference }

public partial class CenterPreview : UserControl, IImageToolHost
{
    private IWorkspaceService? _workspace;
    private IThumbnailService? _thumbs;
    private IImageMetaService? _meta;
    private IHistoryService? _history;
    private DevelopClipboard? _clipboard;
    private IReadOnlyList<EditOperation>? _tempPreviewOps;
    private Color _maskOverlayColor = Color.FromArgb(0x80, 0xFF, 0x00, 0x00); // Default Red
    private readonly DevelopRenderer _renderer = new();
    private LighttableMode _mode = LighttableMode.Single;
    private bool _isDraggingSplit;
    private double _splitPercent = 0.5;
    private bool _externalAfterActive; // true when plugin (Upscaler...) pushes "after" image

    // Reference view state
    private string? _referenceImagePath;

    // Zoom/pan loupe state
    private double _zoom = 1.0;
    private bool _isPanning;
    private Point _panStartMouse;
    private double _panStartX, _panStartY;
    private const double MinZoom = 1.0;
    private const double MaxZoom = 8.0;

    public ObservableCollection<ThumbItem> GridItems { get; private set; } = new();

    public DevelopRenderer Renderer => _renderer;

    public CenterPreview()
    {
        InitializeComponent();
        icGrid.ItemsSource = GridItems;
        Focusable = true;
        paneSingle.SizeChanged += (_, _) =>
        {
            if (_cropMode) DrawCropOverlay();
            RedrawMaskGizmo();
            NotifyViewportChanged();
        };
    }

    public void Bind(IWorkspaceService workspace, IThumbnailService? thumbs = null, IImageMetaService? meta = null, IHistoryService? history = null)
    {
        _workspace = workspace;
        _thumbs = thumbs;
        _meta = meta;
        _history = history;
        _workspace.ActiveImageChanged += OnActiveChanged;
        _workspace.SelectionChanged += OnSelectionChanged;
        _workspace.FolderOpened += OnFolderOpened;
        if (_thumbs != null) _thumbs.ThumbnailReady += OnThumbReady;
        if (_meta != null) _meta.MetaChanged += OnMetaChanged;
        if (_history != null) _history.HistoryChanged += OnHistoryChanged;
        metadataFilterBar.Bind(_workspace);
    }

    /// <summary>Provide DevelopClipboard for grid context menu (call after Bind).</summary>
    public void BindContext(DevelopClipboard clipboard) => _clipboard = clipboard;

    private void OnHistoryChanged(object? sender, HistoryChangedEventArgs e)
    {
        // Only re-render if currently viewed image matches image with changed history.
        var active = _workspace?.ActiveImage;
        // Update "has adjustments" badge on thumbnail.
        bool edited = (_history?.GetPointer(e.ImagePath) ?? 0) > 0;
        foreach (var t in GridItems)
            if (string.Equals(t.ImagePath, e.ImagePath, StringComparison.OrdinalIgnoreCase))
            { t.IsEdited = edited; break; }

        if (string.IsNullOrEmpty(active)) return;
        if (!string.Equals(active, e.ImagePath, StringComparison.OrdinalIgnoreCase)) return;
        if (_externalAfterActive) return; // comparing plugin result, do not overwrite
        _ = RenderDevelopAsync(active);
    }

    private void OnFolderOpened(object? sender, FolderOpenedEventArgs e)
    {
        var paths = e.Images.ToList();
        var meta = _meta;
        var thumbs = _thumbs;
        var history = _history;
        Task.Run(() =>
        {
            var list = new List<ThumbItem>(paths.Count);
            foreach (var p in paths)
            {
                var item = new ThumbItem(p);
                if (meta != null) item.ApplyMeta(meta.Get(p));
                if (history != null) item.IsEdited = history.GetPointer(p) > 0;
                var cached = thumbs?.TryGetThumbnailPath(p, 256);
                if (cached != null) item.SetThumb(cached);
                list.Add(item);
            }
            Dispatcher.BeginInvoke(() =>
            {
                _stacked = false;
                _allGridBackup = null;
                btnStack.Background = ThemeManager.GetBrush("BgHoverBrush");
                GridItems = new ObservableCollection<ThumbItem>(list);
                icGrid.ItemsSource = GridItems;
            });
        });
    }

    private void OnThumbReady(object? sender, ThumbnailReadyEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var t in GridItems)
                if (string.Equals(t.ImagePath, e.ImagePath, StringComparison.OrdinalIgnoreCase))
                { t.SetThumb(e.ThumbnailPath); break; }
        });
    }

    private void OnMetaChanged(object? sender, ImageMetaChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var t in GridItems)
                if (string.Equals(t.ImagePath, e.ImagePath, StringComparison.OrdinalIgnoreCase))
                { t.ApplyMeta(e.Meta); break; }
        });
    }

    private void OnActiveChanged(object? sender, ImageSelectedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var t in GridItems)
                t.IsActive = string.Equals(t.ImagePath, e.CurrentPath, StringComparison.OrdinalIgnoreCase);

            _tempPreviewOps = null; // Reset temp preview ops
            ClearResult(); // image changed -> clear after result
            ResetZoom();   // image changed -> reset to fit
            UpdatePreview(e.CurrentPath);
            ActiveImageChanged?.Invoke(this, e.CurrentPath);
        });
    }

    private void OnSelectionChanged(object? sender, BatchSelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            txtSelection.Text = $"Selection: {e.Selection.Count}";
            var set = new HashSet<string>(e.Selection, StringComparer.OrdinalIgnoreCase);
            foreach (var t in GridItems) t.IsSelected = set.Contains(t.ImagePath);
            if (_mode == LighttableMode.Cull) RebuildCullView();
        });
    }

    private void UpdatePreview(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            imgPreview.Source = null;
            imgFull.Source = null;
            txtPlaceholder.Visibility = Visibility.Visible;
            txtFile.Text = "(No photo selected)";
            txtMeta.Text = "";
            return;
        }

        // If image has edit history and decoder supports it, render via non-destructive pipeline.
        int pointer = _history?.GetPointer(path) ?? 0;
        if (pointer > 0 && _renderer.CanDecode(path))
        {
            _ = RenderDevelopAsync(path);
            return;
        }

        // RAW: WPF BitmapImage cannot decode directly -> render via pipeline (extract embedded JPEG preview).
        if (ZeroVision.Imaging.RawPreviewExtractor.IsRawExtension(path) && _renderer.CanDecode(path))
        {
            _ = RenderDevelopAsync(path);
            return;
        }

        // Default: fast display via BitmapImage (proxy decode width to conserve RAM).
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            imgPreview.Source = bmp;
            imgFull.Source = bmp;
            txtPlaceholder.Visibility = Visibility.Collapsed;
            txtFile.Text = Path.GetFileName(path);
            var fi = new FileInfo(path);
            txtMeta.Text = $"{bmp.PixelWidth} x {bmp.PixelHeight}  |  {fi.Length / 1024.0:N0} KB";
            NotifyViewportChanged();
        }
        catch
        {
            imgPreview.Source = null;
            imgFull.Source = null;
            txtPlaceholder.Visibility = Visibility.Visible;
            NotifyViewportChanged();
        }
    }

    /// <summary>Render photo via Develop pipeline (proxy linear-light) and push to preview.</summary>
    private async Task RenderDevelopAsync(string path)
    {
        var history = _history;
        IReadOnlyList<EditOperation> ops;
        int pointer;
        if (_tempPreviewOps != null)
        {
            ops = _tempPreviewOps;
            pointer = ops.Count;
        }
        else
        {
            ops = history?.GetStack(path) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
            pointer = history?.GetPointer(path) ?? 0;
        }

        // In crop mode: display UNCROPPED image (reset rectangle, retain straighten) so overlay matches coordinates.
        if (_cropMode) ops = StripCropRect(ops, pointer);

        try
        {
            var bmp = await _renderer.RenderPreviewAsync(path, ops, pointer);
            if (bmp == null) return; // cancelled by newer job
            // Only apply if currently viewed image matches.
            if (!string.Equals(_workspace?.ActiveImage, path, StringComparison.OrdinalIgnoreCase)) return;
            imgPreview.Source = bmp;
            imgFull.Source = bmp;
            txtPlaceholder.Visibility = Visibility.Collapsed;
            txtFile.Text = Path.GetFileName(path);
            txtMeta.Text = $"{bmp.PixelWidth} x {bmp.PixelHeight}  |  edit · {pointer} step(s)";
            if (_cropMode) DrawCropOverlay();
            RefreshClipOverlayIfActive();
            RefreshPeakOverlayIfActive();
            NotifyViewportChanged();
        }
        catch { }
    }

    private void SetMode(LighttableMode m) => SwitchMode(m);

    public void SwitchMode(LighttableMode m)
    {
        _mode = m;
        // Leave compare mode when switching mode.
        if (_compareMode)
        {
            _compareMode = false;
            ctrlCompare.Visibility = Visibility.Collapsed;
            ctrlCompare.Clear();
            btnCompare.Background = ThemeManager.GetBrush("BgHoverBrush");
        }
        paneSingle.Visibility = m == LighttableMode.Single ? Visibility.Visible : Visibility.Collapsed;
        paneGridHost.Visibility = m == LighttableMode.Grid ? Visibility.Visible : Visibility.Collapsed;
        btnToggleFilter.Visibility = m == LighttableMode.Grid ? Visibility.Visible : Visibility.Collapsed;
        paneCull.Visibility = m == LighttableMode.Cull ? Visibility.Visible : Visibility.Collapsed;
        paneFull.Visibility = m == LighttableMode.Full ? Visibility.Visible : Visibility.Collapsed;
        paneReference.Visibility = m == LighttableMode.Reference ? Visibility.Visible : Visibility.Collapsed;
        if (m == LighttableMode.Cull) RebuildCullView();
        if (m == LighttableMode.Reference) UpdateReferenceView();
        ModeChanged?.Invoke(this, m);
    }

    private void BtnToggleFilter_Click(object sender, RoutedEventArgs e)
    {
        metadataFilterBar.Visibility = metadataFilterBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }

    public LighttableMode CurrentMode => _mode;
    public event EventHandler<LighttableMode>? ModeChanged;

    private void RebuildCullView()
    {
        paneCull.Children.Clear();
        if (_workspace == null) return;
        var sel = _workspace.Selection.Take(4).ToList();
        if (sel.Count == 0 && _workspace.ActiveImage != null) sel.Add(_workspace.ActiveImage);
        if (sel.Count == 0) return;

        paneCull.Columns = sel.Count <= 1 ? 1 : (sel.Count <= 2 ? 2 : 2);
        paneCull.Rows = sel.Count <= 2 ? 1 : 2;

        foreach (var p in sel)
        {
            var img = new Image { Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(4) };
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(p);
                bmp.DecodePixelWidth = 1600;
                bmp.EndInit();
                bmp.Freeze();
                img.Source = bmp;
            }
            catch { }
            var border = new Border
            {
                BorderBrush = ThemeManager.GetBrush("BorderHoverBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Child = img
            };
            paneCull.Children.Add(border);
        }
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.G: SetMode(LighttableMode.Grid); e.Handled = true; break;
            case Key.E: SetMode(LighttableMode.Single); e.Handled = true; break;
            case Key.C: SetMode(LighttableMode.Cull); e.Handled = true; break;
            case Key.F: SetMode(LighttableMode.Full); e.Handled = true; break;
            case Key.R: ToggleCropMode(); e.Handled = true; break;
            case Key.X:
                if (_cropMode)
                {
                    SwapCropOrientation();
                    e.Handled = true;
                }
                break;
            case Key.O:
                if (_cropMode)
                {
                    CycleCropGuide();
                    e.Handled = true;
                }
                else if (_brushMask != null)
                {
                    CycleMaskOverlayColor();
                    e.Handled = true;
                }
                break;
            case Key.J: ToggleClipOverlay(); e.Handled = true; break;
            case Key.K: TogglePeakOverlay(); e.Handled = true; break; // focus peaking
            case Key.OemOpenBrackets: _developPanel?.RotateActive(-1); e.Handled = true; break; // [
            case Key.OemCloseBrackets: _developPanel?.RotateActive(1); e.Handled = true; break;  // ]
            case Key.Y: // Y: toggle before/after comparison side-by-side (unless Ctrl = redo)
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) { ToggleCompareMode(); e.Handled = true; }
                break;
            case Key.Oem5: // key "\": Grid -> toggle Filter Bar; Single -> view original (before)
                if (_mode == LighttableMode.Grid)
                {
                    metadataFilterBar.Visibility = metadataFilterBar.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    if (!_showingBefore) ShowBefore(true);
                }
                e.Handled = true;
                break;
            case Key.Z: // toggle 100% / fit (Lightroom style)
                ToggleZoom();
                e.Handled = true;
                break;
            case Key.OemPlus:
            case Key.Add:
                StepZoom(1.25); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:
                StepZoom(1 / 1.25); e.Handled = true; break;
            case Key.Escape:
                if (_cropMode) { ToggleCropMode(); e.Handled = true; }       // exit crop first
                else if (_zoom > 1.0) { ResetZoom(); e.Handled = true; }
                else if (_compareMode) { ToggleCompareMode(); e.Handled = true; }
                else if (_mode == LighttableMode.Full) { SetMode(LighttableMode.Single); e.Handled = true; }
                break;
            case Key.Enter: // Enter applies crop when in crop mode
                if (_cropMode) { ToggleCropMode(); e.Handled = true; }
                break;
            case Key.Space: // hold Space to pan (Photoshop style)
                if (!_spaceHeld)
                {
                    _spaceHeld = true;
                    if (_zoom > 1.0) paneSingle.Cursor = System.Windows.Input.Cursors.Hand;
                }
                e.Handled = true;
                break;
        }
    }

    private bool _spaceHeld;

    private void UserControl_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Oem5 && _showingBefore) { ShowBefore(false); e.Handled = true; }
        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
            if (!_isPanning) paneSingle.Cursor = System.Windows.Input.Cursors.Arrow;
            e.Handled = true;
        }
    }

    private bool _showingBefore;

    /// <summary>Temporarily show original image when holding "\"; release to restore edited view.</summary>
    private void ShowBefore(bool before)
    {
        _showingBefore = before;
        var path = _workspace?.ActiveImage;
        if (string.IsNullOrEmpty(path) || _externalAfterActive) { _showingBefore = false; return; }
        if (before)
        {
            // render pointer=0 (original image).
            _ = RenderAtPointerAsync(path, 0);
            txtFile.Text = System.IO.Path.GetFileName(path) + "  [BEFORE]";
        }
        else
        {
            _ = RenderDevelopAsync(path);
        }
    }

    private async Task RenderAtPointerAsync(string path, int pointer)
    {
        if (!_renderer.CanDecode(path)) return;
        try
        {
            var ops = _history?.GetStack(path) ?? (IReadOnlyList<EditOperation>)Array.Empty<EditOperation>();
            var bmp = await _renderer.RenderPreviewAsync(path, ops, pointer);
            if (bmp == null) return;
            if (!string.Equals(_workspace?.ActiveImage, path, StringComparison.OrdinalIgnoreCase)) return;
            imgPreview.Source = bmp;
            imgFull.Source = bmp;
        }
        catch { }
    }

    private void BtnModeSingle_Click(object sender, RoutedEventArgs e) => SetMode(LighttableMode.Single);
    private void BtnModeGrid_Click(object sender, RoutedEventArgs e) => SetMode(LighttableMode.Grid);
    private void BtnModeCull_Click(object sender, RoutedEventArgs e) => SetMode(LighttableMode.Cull);
    private void BtnModeFull_Click(object sender, RoutedEventArgs e) => SetMode(LighttableMode.Full);

    private void GridItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ThumbItem item && _workspace != null)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl)
            {
                if (_workspace.Selection.Contains(item.ImagePath)) _workspace.RemoveFromSelection(item.ImagePath);
                else _workspace.AddToSelection(item.ImagePath);
            }
            else
            {
                _workspace.SetSelection(new[] { item.ImagePath });
            }
            _workspace.SetActiveImage(item.ImagePath);
            if (e.ClickCount >= 2) SetMode(LighttableMode.Single);
        }
    }

    private void GridItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ThumbItem item &&
            _workspace != null && _meta != null && _history != null && _clipboard != null)
        {
            if (!_workspace.Selection.Contains(item.ImagePath))
            {
                _workspace.SetSelection(new[] { item.ImagePath });
                _workspace.SetActiveImage(item.ImagePath);
            }
            fe.ContextMenu = ImageContextMenu.Build(item.ImagePath, _workspace, _meta, _history, _clipboard);
            fe.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    // ===== Before/After splitter =====
    private void PaneSingle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (TryHandleHealClick(e)) { e.Handled = true; return; }
        if (TryHandleWbPick(e)) { e.Handled = true; return; }
        if (TryHandleTatMouseDown(e)) { e.Handled = true; return; }
        // Space + left drag = pan (when zoomed).
        if (_spaceHeld && _zoom > 1.0 && imgPreview.Source != null)
        {
            _isPanning = true;
            _panStartMouse = e.GetPosition(paneSingle);
            _panStartX = zoomPan.X;
            _panStartY = zoomPan.Y;
            paneSingle.CaptureMouse();
            paneSingle.Cursor = System.Windows.Input.Cursors.ScrollAll;
            e.Handled = true;
            return;
        }
        if (afterBadge.Visibility != Visibility.Visible) return;
        _isDraggingSplit = true;
        paneSingle.CaptureMouse();
        UpdateSplitFromPoint(e.GetPosition(paneSingle));
        e.Handled = true;
    }

    private void PaneSingle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingTat) { TryHandleTatMouseMove(e); return; }
        if (_isPanning)
        {
            var pos = e.GetPosition(paneSingle);
            zoomPan.X = _panStartX + (pos.X - _panStartMouse.X);
            zoomPan.Y = _panStartY + (pos.Y - _panStartMouse.Y);
            ClampPan();
            SyncAfterTransform();
            return;
        }
        if (!_isDraggingSplit) return;
        UpdateSplitFromPoint(e.GetPosition(paneSingle));
    }

    private void PaneSingle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingTat) { TryHandleTatMouseUp(e); return; }
        // finish Space-pan (left button).
        if (_isPanning)
        {
            _isPanning = false;
            paneSingle.ReleaseMouseCapture();
            paneSingle.Cursor = _spaceHeld ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
            e.Handled = true;
            return;
        }
        if (!_isDraggingSplit) return;
        _isDraggingSplit = false;
        paneSingle.ReleaseMouseCapture();
    }

    // ===== Zoom / Pan loupe =====
    private void PaneSingle_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (imgPreview.Source == null) return;
        double old = _zoom;
        double factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(_zoom - old) < 1e-6) return;

        // Zoom around cursor: keep point under mouse stationary.
        var p = e.GetPosition(imgPreview);
        double scaleRatio = _zoom / old;
        zoomPan.X = (zoomPan.X - p.X) * scaleRatio + p.X;
        zoomPan.Y = (zoomPan.Y - p.Y) * scaleRatio + p.Y;
        zoomScale.ScaleX = zoomScale.ScaleY = _zoom;
        ClampPan();
        SyncAfterTransform();
        UpdateZoomBadge();
        e.Handled = true;
    }

    private void PaneSingle_PanStart(object sender, MouseButtonEventArgs e)
    {
        if (_zoom <= 1.0 || imgPreview.Source == null) return;
        _isPanning = true;
        _panStartMouse = e.GetPosition(paneSingle);
        _panStartX = zoomPan.X;
        _panStartY = zoomPan.Y;
        paneSingle.CaptureMouse();
        paneSingle.Cursor = System.Windows.Input.Cursors.ScrollAll;
        e.Handled = true;
    }

    private void PaneSingle_PanEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;
        _isPanning = false;
        paneSingle.ReleaseMouseCapture();
        paneSingle.Cursor = System.Windows.Input.Cursors.Arrow;
        e.Handled = true;
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        zoomScale.ScaleX = zoomScale.ScaleY = 1.0;
        zoomPan.X = zoomPan.Y = 0;
        SyncAfterTransform();
        UpdateZoomBadge();
    }

    /// <summary>Toggle between fit (1.0) and 100% actual pixels, centered.</summary>
    private void ToggleZoom()
    {
        if (imgPreview.Source == null) return;
        if (_zoom > 1.001) { ResetZoom(); return; }
        // estimate zoom to achieve 100% actual pixels.
        double target = 2.0;
        if (imgPreview.Source is System.Windows.Media.Imaging.BitmapSource bs && imgPreview.ActualWidth > 0)
        {
            double fitW = imgPreview.ActualWidth;
            target = Math.Clamp(bs.PixelWidth / fitW, MinZoom, MaxZoom);
        }
        ZoomToCenter(target);
    }

    private void StepZoom(double factor)
    {
        if (imgPreview.Source == null) return;
        ZoomToCenter(Math.Clamp(_zoom * factor, MinZoom, MaxZoom));
    }

    private void ZoomToCenter(double newZoom)
    {
        double old = _zoom;
        _zoom = newZoom;
        var c = new Point(imgPreview.ActualWidth / 2, imgPreview.ActualHeight / 2);
        double ratio = _zoom / old;
        zoomPan.X = (zoomPan.X - c.X) * ratio + c.X;
        zoomPan.Y = (zoomPan.Y - c.Y) * ratio + c.Y;
        zoomScale.ScaleX = zoomScale.ScaleY = _zoom;
        ClampPan();
        SyncAfterTransform();
        UpdateZoomBadge();
    }

    private void ClampPan()
    {
        // Clamp pan to prevent image from drifting offscreen.
        double w = imgPreview.ActualWidth, h = imgPreview.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double maxX = w * (_zoom - 1);
        double maxY = h * (_zoom - 1);
        zoomPan.X = Math.Clamp(zoomPan.X, -maxX, 0);
        zoomPan.Y = Math.Clamp(zoomPan.Y, -maxY, 0);
    }

    private void SyncAfterTransform()
    {
        // Synchronize transform for "after" image so split comparison matches.
        zoomScaleAfter.ScaleX = zoomScale.ScaleX;
        zoomScaleAfter.ScaleY = zoomScale.ScaleY;
        zoomPanAfter.X = zoomPan.X;
        zoomPanAfter.Y = zoomPan.Y;
        if (_clipOverlay) SyncClipTransform();
        if (_peakOverlay) SyncPeakTransform();
        RedrawMaskGizmo();
    }

    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;

    public void ZoomToMode(string mode)
    {
        if (imgPreview.Source == null) return;
        switch (mode.ToUpperInvariant())
        {
            case "FIT":
                ResetZoom();
                break;
            case "FILL":
                if (imgPreview.ActualWidth > 0 && imgPreview.ActualHeight > 0 && paneSingle.ActualWidth > 0 && paneSingle.ActualHeight > 0)
                {
                    double ratioW = paneSingle.ActualWidth / imgPreview.ActualWidth;
                    double ratioH = paneSingle.ActualHeight / imgPreview.ActualHeight;
                    ZoomToCenter(Math.Clamp(Math.Max(ratioW, ratioH), MinZoom, MaxZoom));
                }
                break;
            case "1:1":
                if (imgPreview.Source is System.Windows.Media.Imaging.BitmapSource bs1 && imgPreview.ActualWidth > 0)
                {
                    double target = bs1.PixelWidth / imgPreview.ActualWidth;
                    ZoomToCenter(Math.Clamp(target, MinZoom, MaxZoom));
                }
                else ZoomToCenter(2.0);
                break;
            case "2:1":
                if (imgPreview.Source is System.Windows.Media.Imaging.BitmapSource bs2 && imgPreview.ActualWidth > 0)
                {
                    double target = (bs2.PixelWidth / imgPreview.ActualWidth) * 2.0;
                    ZoomToCenter(Math.Clamp(target, MinZoom, MaxZoom));
                }
                else ZoomToCenter(4.0);
                break;
        }
    }

    public void PanToNormalized(double normCenterX, double normCenterY)
    {
        if (imgPreview.Source == null) return;
        if (_zoom <= 1.001)
        {
            ZoomToMode("1:1");
        }

        double paneW = paneSingle.ActualWidth;
        double paneH = paneSingle.ActualHeight;
        double imgW = imgPreview.ActualWidth;
        double imgH = imgPreview.ActualHeight;
        if (paneW <= 0 || paneH <= 0 || imgW <= 0 || imgH <= 0) return;

        zoomPan.X = (paneW / 2.0) - (normCenterX * imgW) * _zoom;
        zoomPan.Y = (paneH / 2.0) - (normCenterY * imgH) * _zoom;
        ClampPan();
        SyncAfterTransform();
        UpdateZoomBadge();
    }

    private void UpdateZoomBadge()
    {
        if (_zoom > 1.001)
        {
            txtZoom.Text = $"{_zoom * 100:0}%";
            zoomBadge.Visibility = Visibility.Visible;
            UpdateNavigator();
        }
        else
        {
            zoomBadge.Visibility = Visibility.Collapsed;
            navigatorOverlay.Visibility = Visibility.Collapsed;
        }
        NotifyViewportChanged();
    }

    private void NotifyViewportChanged()
    {
        double normX = 0, normY = 0, normW = 1.0, normH = 1.0;
        if (_zoom > 1.001 && imgPreview.ActualWidth > 0 && imgPreview.ActualHeight > 0)
        {
            normX = -zoomPan.X / (_zoom * imgPreview.ActualWidth);
            normY = -zoomPan.Y / (_zoom * imgPreview.ActualHeight);
            normW = paneSingle.ActualWidth / (_zoom * imgPreview.ActualWidth);
            normH = paneSingle.ActualHeight / (_zoom * imgPreview.ActualHeight);
        }
        ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(imgPreview.Source, _zoom, normX, normY, normW, normH));
    }

    // ===== Navigator mini-map =====
    private bool _navigatorDragging;

    private void UpdateNavigator()
    {
        if (imgPreview.Source == null || _zoom <= 1.001)
        {
            navigatorOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        navigatorOverlay.Visibility = Visibility.Visible;
        navigatorThumb.Source = imgPreview.Source;

        // Calculate viewport rectangle
        double paneW = paneSingle.ActualWidth;
        double paneH = paneSingle.ActualHeight;
        if (paneW <= 0 || paneH <= 0) return;

        double imgW = imgPreview.ActualWidth;
        double imgH = imgPreview.ActualHeight;
        if (imgW <= 0 || imgH <= 0) return;

        // Navigator size
        double navW = 160;
        double navH = 100;

        // Image ratio in navigator
        double imgAspect = imgW / imgH;
        double navAspect = navW / navH;
        double scale, offsetX = 0, offsetY = 0;
        if (imgAspect > navAspect)
        {
            scale = navW / imgW;
            offsetY = (navH - imgH * scale) / 2;
        }
        else
        {
            scale = navH / imgH;
            offsetX = (navW - imgW * scale) / 2;
        }

        // Viewport rectangle in navigator coordinates
        double vpW = (paneW / _zoom) * scale;
        double vpH = (paneH / _zoom) * scale;
        double vpX = offsetX + (-zoomPan.X / _zoom) * scale;
        double vpY = offsetY + (-zoomPan.Y / _zoom) * scale;

        // Clamp viewport trong navigator bounds
        vpX = Math.Max(0, Math.Min(vpX, navW - vpW));
        vpY = Math.Max(0, Math.Min(vpY, navH - vpH));

        navigatorViewport.Children.Clear();
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(4, vpW),
            Height = Math.Max(4, vpH),
            Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0x4F, 0xC3, 0xF7)),
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x4F, 0xC3, 0xF7))
        };
        System.Windows.Controls.Canvas.SetLeft(rect, vpX);
        System.Windows.Controls.Canvas.SetTop(rect, vpY);
        navigatorViewport.Children.Add(rect);
    }

    private void Navigator_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _navigatorDragging = true;
            NavigateFromNavigator(e.GetPosition(navigatorContent));
            navigatorOverlay.CaptureMouse();
        }
    }

    private void Navigator_MouseMove(object sender, MouseEventArgs e)
    {
        if (_navigatorDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            NavigateFromNavigator(e.GetPosition(navigatorContent));
        }
    }

    private void Navigator_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _navigatorDragging = false;
        navigatorOverlay.ReleaseMouseCapture();
    }

    private void NavigateFromNavigator(Point navPoint)
    {
        if (imgPreview.Source == null || _zoom <= 1.0) return;

        double navW = 160;
        double navH = 100;
        double imgW = imgPreview.ActualWidth;
        double imgH = imgPreview.ActualHeight;
        if (imgW <= 0 || imgH <= 0) return;

        double imgAspect = imgW / imgH;
        double navAspect = navW / navH;
        double scale, offsetX = 0, offsetY = 0;
        if (imgAspect > navAspect)
        {
            scale = navW / imgW;
            offsetY = (navH - imgH * scale) / 2;
        }
        else
        {
            scale = navH / imgH;
            offsetX = (navW - imgW * scale) / 2;
        }

        // Convert navigator click point to image coordinates
        double imgX = (navPoint.X - offsetX) / scale;
        double imgY = (navPoint.Y - offsetY) / scale;

        // Calculate pan to center clicked point in viewport
        double paneW = paneSingle.ActualWidth;
        double paneH = paneSingle.ActualHeight;
        zoomPan.X = -(imgX * _zoom - paneW / 2);
        zoomPan.Y = -(imgY * _zoom - paneH / 2);

        SyncAfterTransform();
        UpdateNavigator();
    }

    private void UpdateSplitFromPoint(Point p)
    {
        double w = paneSingle.ActualWidth;
        if (w <= 0) return;
        _splitPercent = Math.Clamp(p.X / w, 0, 1);
        UpdateSplitClip();
    }

    private void UpdateSplitClip()
    {
        if (imgAfter.Source == null) { imgAfter.Clip = null; borderSplitLine.Margin = new Thickness(0); return; }
        double w = paneSingle.ActualWidth;
        double h = paneSingle.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double clipXScreen = w * _splitPercent; // split line position in paneSingle coordinates

        // imgAfter has Margin + RenderTransform. Clip applied in LOCAL coordinates of imgAfter
        // (pre-transform), so inverse-transform screen coordinates to local:
        //   screen = marginLeft + (local * scale + panX)  =>  local = (screen - marginLeft - panX) / scale
        double marginLeft = imgAfter.Margin.Left;
        double s = zoomScaleAfter.ScaleX > 1e-6 ? zoomScaleAfter.ScaleX : 1.0;
        double tx = zoomPanAfter.X;
        double localX = (clipXScreen - marginLeft - tx) / s;

        // Clip covers right side of localX.
        imgAfter.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(localX, -100000, 200000, 200000));
        borderSplitLine.Margin = new Thickness(clipXScreen, 0, 0, 0);
    }

    private void BtnClearAfter_Click(object sender, RoutedEventArgs e) => ClearResult();

    // ===== Reference View =====
    private void BtnSetReference_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Reference Photo",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.tiff;*.tif;*.bmp;*.webp|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _referenceImagePath = dlg.FileName;
            UpdateReferenceView();
        }
    }

    public void SetReferenceImage(string? path)
    {
        _referenceImagePath = path;
        if (_mode == LighttableMode.Reference) UpdateReferenceView();
    }

    private async void UpdateReferenceView()
    {
        if (_referenceImagePath == null || !File.Exists(_referenceImagePath))
        {
            imgReference.Source = null;
            return;
        }

        // Load reference image
        try
        {
            var bmp = await _renderer.RenderPreviewAsync(_referenceImagePath,
                Array.Empty<EditOperation>(), 0);
            imgReference.Source = bmp;
        }
        catch { }

        // Update current image
        var active = _workspace?.ActiveImage;
        if (active != null)
        {
            var ops = _history?.GetStack(active) ?? Array.Empty<EditOperation>();
            var currentBmp = await _renderer.RenderPreviewAsync(active, ops, _history?.GetPointer(active) ?? 0);
            imgRefCurrent.Source = currentBmp;
        }
    }

    // ===== IImageToolHost =====
    public string? ActiveImagePath => _workspace?.ActiveImage;
    public event EventHandler<string?>? ActiveImageChanged;

    public void ShowResult(string? resultPath, byte[]? imageBytes = null)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                BitmapImage? bmp = null;
                if (imageBytes != null && imageBytes.Length > 0)
                {
                    bmp = new BitmapImage();
                    using var ms = new MemoryStream(imageBytes);
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                else if (!string.IsNullOrEmpty(resultPath) && File.Exists(resultPath))
                {
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(resultPath);
                    bmp.EndInit();
                    bmp.Freeze();
                }
                if (bmp == null) return;

                imgAfter.Source = bmp;
                imgAfter.Visibility = Visibility.Visible;
                borderSplitLine.Visibility = Visibility.Visible;
                afterBadge.Visibility = Visibility.Visible;
                _externalAfterActive = true;
                _splitPercent = 0.5;
                SwitchMode(LighttableMode.Single);
                UpdateSplitClip();
            }
            catch { }
        });
    }

    public void ClearResult()
    {
        Dispatcher.BeginInvoke(() =>
        {
            imgAfter.Source = null;
            imgAfter.Visibility = Visibility.Collapsed;
            imgAfter.Clip = null;
            borderSplitLine.Visibility = Visibility.Collapsed;
            afterBadge.Visibility = Visibility.Collapsed;
            _externalAfterActive = false;
        });
    }

    public void ReportProgress(int percent, string? status = null)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Raise event for MainWindow status bar; update txtMeta locally.
            ProgressReported?.Invoke(this, (percent, status));
            if (percent < 0) { txtMeta.Text = ""; return; }
            if (status != null) txtMeta.Text = $"{status} {percent}%";
        });
    }

    /// <summary>Emit progress (percent, status) for host status bar. percent &lt; 0 = hide.</summary>
    public event EventHandler<(int Percent, string? Status)>? ProgressReported;

    public void SetTemporaryOperations(IReadOnlyList<EditOperation>? ops, string? styleName = null)
    {
        _tempPreviewOps = ops;
        if (ops != null && !string.IsNullOrEmpty(styleName))
        {
            txtPresetPreviewName.Text = styleName;
            badgePresetPreview.Visibility = Visibility.Visible;
        }
        else
        {
            badgePresetPreview.Visibility = Visibility.Collapsed;
        }

        var active = _workspace?.ActiveImage;
        if (!string.IsNullOrEmpty(active))
        {
            _ = RenderDevelopAsync(active);
        }
    }

    private void CycleMaskOverlayColor()
    {
        if (_maskOverlayColor.R == 255 && _maskOverlayColor.G == 0 && _maskOverlayColor.B == 0) // Red -> Green
            _maskOverlayColor = Color.FromArgb(0x80, 0x00, 0xFF, 0x00);
        else if (_maskOverlayColor.G == 255 && _maskOverlayColor.R == 0) // Green -> Blue
            _maskOverlayColor = Color.FromArgb(0x80, 0x00, 0x00, 0xFF);
        else if (_maskOverlayColor.B == 255 && _maskOverlayColor.R == 0) // Blue -> White
            _maskOverlayColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);
        else if (_maskOverlayColor.R == 255 && _maskOverlayColor.G == 255) // White -> Black
            _maskOverlayColor = Color.FromArgb(0x80, 0x00, 0x00, 0x00);
        else // Black -> Red
            _maskOverlayColor = Color.FromArgb(0x80, 0xFF, 0x00, 0x00);

        RedrawBrushOverlay(); // Redraw brush strokes with new color
        
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.ShowToast($"Mask overlay color: {GetMaskColorName(_maskOverlayColor)}");
        }
    }

    private string GetMaskColorName(Color c)
    {
        if (c.R == 255 && c.G == 0 && c.B == 0) return "Red";
        if (c.G == 255 && c.R == 0 && c.B == 0) return "Green";
        if (c.B == 255 && c.R == 0 && c.G == 0) return "Blue";
        if (c.R == 255 && c.G == 255 && c.B == 255) return "White";
        return "Black";
    }
}
