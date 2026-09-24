using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZVision.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ZVision.Host.Workspace;

public partial class InfoPanel : UserControl
{
    private IWorkspaceService? _workspace;
    private IImageMetaService? _meta;
    private ISettingsService? _settings;
    private CancellationTokenSource? _cts;
    private string? _currentPath;

    public ObservableCollection<KeywordVm> Keywords { get; } = new();
    public ObservableCollection<KeywordVm> KeywordSuggestions { get; } = new();

    public InfoPanel()
    {
        InitializeComponent();
        icKeywords.ItemsSource = Keywords;
        icKeywordSuggest.ItemsSource = KeywordSuggestions;
    }

    public void Bind(IWorkspaceService ws)
    {
        _workspace = ws;
        _workspace.ActiveImageChanged += (s, e) => Refresh(e.CurrentPath);
    }

    /// <summary>Bind kèm meta + settings để bật trình sửa Keywords (D6.4).</summary>
    public void Bind(IWorkspaceService ws, IImageMetaService meta, ISettingsService settings)
    {
        _meta = meta;
        _settings = settings;
        _meta.MetaChanged += (s, e) =>
        {
            if (string.Equals(e.ImagePath, _currentPath, System.StringComparison.OrdinalIgnoreCase))
                Dispatcher.BeginInvoke(() =>
                {
                    LoadKeywords(e.ImagePath);
                    UpdateCurationUi(e.Meta);
                });
        };
        Bind(ws);
    }

    private void Refresh(string? path)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _currentPath = path;

        Dispatcher.BeginInvoke(() =>
        {
            ctrlExifCard.Clear();
            ctrlDominantPalette.Clear();
            ctrlHistScope.Reset();
            txtHistEmpty.Visibility = Visibility.Visible;
            btnSaveMeta.IsEnabled = false;
            ClearMetaFields();
            LoadKeywords(path);
            if (!string.IsNullOrEmpty(path) && _meta != null)
            {
                UpdateCurationUi(_meta.Get(path));
            }
            else
            {
                UpdateCurationUi(null);
            }
        });

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        Task.Run(() =>
        {
            try
            {
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(path);

                // EXIF
                var rows = new List<ZeroUI.Wpf.Editors.ExifTelemetryItem>();
                var fi = new FileInfo(path);
                double gpsLat = 0, gpsLon = 0;
                bool hasGps = false;
                rows.Add(new ZeroUI.Wpf.Editors.ExifTelemetryItem("File", fi.Name));
                rows.Add(new ZeroUI.Wpf.Editors.ExifTelemetryItem("Size", $"{fi.Length / 1024.0:N0} KB"));
                rows.Add(new ZeroUI.Wpf.Editors.ExifTelemetryItem("Pixels", $"{img.Width} x {img.Height}"));
                rows.Add(new ZeroUI.Wpf.Editors.ExifTelemetryItem("Modified", fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm")));

                if (img.Metadata.ExifProfile != null)
                {
                    // GPS (8.5): hiển thị toạ độ + lưu để mở bản đồ.
                    if (ZVision.Shared.ExifReader.TryReadGps(img.Metadata.ExifProfile, out var gLat, out var gLon))
                    {
                        rows.Insert(0, new ZeroUI.Wpf.Editors.ExifTelemetryItem("GPS", ZVision.Shared.GpsHelper.Format(gLat, gLon)));
                        gpsLat = gLat; gpsLon = gLon; hasGps = true;
                    }

                    foreach (var v in img.Metadata.ExifProfile.Values)
                    {
                        if (ct.IsCancellationRequested) return;
                        var val = v.GetValue()?.ToString();
                        if (string.IsNullOrEmpty(val)) continue;
                        if (val.Length > 80) val = val.Substring(0, 80) + "…";
                        rows.Add(new ZeroUI.Wpf.Editors.ExifTelemetryItem(v.Tag.ToString() ?? "", val));
                    }
                }

                // Đọc metadata tổng quát (camera/lens/exposure)
                var ci = ZVision.Shared.ExifReader.GetOrCreate(path);

                // Bảng màu chủ đạo (K-Means trên ảnh đã load).
                var swatches = ZVision.Shared.DominantColors.Extract(img, k: 6);
                if (ct.IsCancellationRequested) return;

                // Histogram (downscale rồi tính)
                using var small = img.Clone(c => c.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(256, 256),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
                    Sampler = KnownResamplers.Box
                }));

                int[] r = new int[256], g = new int[256], b = new int[256];
                small.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            r[row[x].R]++;
                            g[row[x].G]++;
                            b[row[x].B]++;
                        }
                    }
                });

                if (ct.IsCancellationRequested) return;

                // Cảnh báo clip: % pixel cháy sáng (>=254 cả 3 kênh) / mất chi tiết tối (<=1).
                long total = 0; for (int i = 0; i < 256; i++) total += r[i];
                long hiClip = r[255] + r[254] + g[255] + g[254] + b[255] + b[254];
                long loClip = r[0] + r[1] + g[0] + g[1] + b[0] + b[1];
                double hiPct = total > 0 ? hiClip / (3.0 * total) * 100.0 : 0;
                double loPct = total > 0 ? loClip / (3.0 * total) * 100.0 : 0;
                bool hiWarn = hiPct > 0.5, loWarn = loPct > 0.5;

                Dispatcher.BeginInvoke(() =>
                {
                    if (ct.IsCancellationRequested) return;

                    // 1. Cập nhật ExifTelemetryCard
                    var camera = string.Join(" ", new[] { ci.CameraMake, ci.CameraModel }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                    ctrlExifCard.SetTelemetry(
                        camera: !string.IsNullOrEmpty(camera) ? camera : null,
                        lens: ci.LensModel,
                        shutter: ci.ShutterSpeed,
                        aperture: ci.Aperture > 0 ? $"f/{ci.Aperture:0.#}" : null,
                        iso: ci.Iso > 0 ? $"ISO {ci.Iso}" : null,
                        focalLength: ci.FocalLength > 0 ? $"{ci.FocalLength:0.#}mm" : null,
                        captureDateTime: ci.DateTaken?.ToString("yyyy-MM-dd HH:mm")
                    );

                    if (hasGps)
                    {
                        ctrlExifCard.SetGps(gpsLat, gpsLon, ZVision.Shared.GpsHelper.Format(gpsLat, gpsLon));
                    }
                    else
                    {
                        ctrlExifCard.ClearGps();
                    }

                    if (hiWarn) rows.Insert(0, new ZeroUI.Wpf.Editors.ExifTelemetryItem("⚠ Highlight clip", $"{hiPct:0.0}%"));
                    if (loWarn) rows.Insert(hiWarn ? 1 : 0, new ZeroUI.Wpf.Editors.ExifTelemetryItem("⚠ Shadow clip", $"{loPct:0.0}%"));
                    ctrlExifCard.SetExifRows(rows);

                    // 2. Histogram Scope
                    ctrlHistScope.SetChannels(r, g, b);
                    ctrlHistScope.ShadowClipPercent = loWarn ? loPct : 0.0;
                    ctrlHistScope.HighlightClipPercent = hiWarn ? hiPct : 0.0;
                    txtHistEmpty.Visibility = Visibility.Collapsed;

                    // 3. Dominant Palette & Color Harmony
                    var pSwatches = swatches.Select(s => new ZeroUI.Wpf.Editors.PaletteSwatchItem(s.R, s.G, s.B, s.PercentText, "Dominant", s.Fraction * 100.0)).ToList();
                    var pSuggestions = new List<ZeroUI.Wpf.Editors.PaletteSwatchItem>();
                    string? adviceText = null;
                    if (swatches.Count > 0)
                    {
                        var top = swatches[0];
                        foreach (var sg in ZVision.Shared.ColorSuggestion.FromDominant(top.R, top.G, top.B))
                            pSuggestions.Add(new ZeroUI.Wpf.Editors.PaletteSwatchItem(sg.R, sg.G, sg.B, sg.Role, sg.Role));

                        var swList = swatches.Select(s => (s.R, s.G, s.B)).ToList();
                        var (score, advice) = ZVision.Shared.ColorSuggestion.AssessContrast(swList);
                        adviceText = $"Color contrast: {score * 100:0}% — {advice}";
                    }
                    ctrlDominantPalette.SetPalette(pSwatches, pSuggestions, adviceText);

                    // 4. Form sửa metadata & keywords
                    LoadMetaFields(path);
                    btnSaveMeta.IsEnabled = true;
                    LoadKeywords(path);
                });
            }
            catch (Exception ex) { ZVision.Shared.AppLog.Error("InfoPanel.Refresh", path, ex); }
        }, ct);
    }

    // ===== Sửa metadata (gộp từ MetaEditor) =====
    private void ClearMetaFields()
    {
        txtDescription.Text = ""; txtArtist.Text = ""; txtCopyright.Text = "";
        txtSoftware.Text = ""; txtMake.Text = ""; txtModel.Text = "";
    }

    private void LoadMetaFields(string path)
    {
        try
        {
            var m = ZVision.Shared.ExifWriter.ReadEditable(path);
            txtDescription.Text = m.GetValueOrDefault("ImageDescription", "");
            txtArtist.Text = m.GetValueOrDefault("Artist", "");
            txtCopyright.Text = m.GetValueOrDefault("Copyright", "");
            txtSoftware.Text = m.GetValueOrDefault("Software", "");
            txtMake.Text = m.GetValueOrDefault("Make", "");
            txtModel.Text = m.GetValueOrDefault("Model", "");
        }
        catch (Exception ex) { ZVision.Shared.AppLog.Warn("InfoPanel.LoadMeta", ex.Message); }
    }

    private void BtnSaveMeta_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath)) return;
        var values = new Dictionary<string, string>
        {
            ["ImageDescription"] = txtDescription.Text,
            ["Artist"] = txtArtist.Text,
            ["Copyright"] = txtCopyright.Text,
            ["Software"] = txtSoftware.Text,
            ["Make"] = txtMake.Text,
            ["Model"] = txtModel.Text,
        };
        bool ok = ZVision.Shared.ExifWriter.Write(_currentPath, values);
        MessageBox.Show(ok ? "Metadata saved to photo successfully." : "Failed to save metadata (see app.log).",
            "Metadata", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        if (ok) Refresh(_currentPath);
    }

    // ===== Keywords / Tags (D6.4) =====

    /// <summary>VM 1 keyword: hiện đoạn lá nhưng giữ full-path để gỡ/thêm chính xác.</summary>
    public sealed class KeywordVm
    {
        public string Full { get; init; } = "";
        public string Leaf { get; init; } = "";
        public static KeywordVm From(string full)
            => new() { Full = full, Leaf = ZVision.Shared.KeywordHelper.LeafName(full) };
    }

    private void LoadKeywords(string? path)
    {
        Keywords.Clear();
        KeywordSuggestions.Clear();
        if (_meta == null || string.IsNullOrEmpty(path)) { UpdateKeywordEmpty(); return; }

        var current = ZVision.Shared.KeywordHelper.NormalizeList(_meta.Get(path).Tags);
        foreach (var k in current) Keywords.Add(KeywordVm.From(k));
        UpdateKeywordEmpty();

        // Gợi ý = (recent + từ điển) trừ tag đã gắn, tối đa 12 mục.
        if (_settings != null)
        {
            var has = new System.Collections.Generic.HashSet<string>(current, System.StringComparer.OrdinalIgnoreCase);
            var seen = new System.Collections.Generic.HashSet<string>(has, System.StringComparer.OrdinalIgnoreCase);
            var pool = new System.Collections.Generic.List<string>();
            pool.AddRange(_settings.Current.RecentTags);
            pool.AddRange(_settings.Current.TagDictionary);
            foreach (var t in pool)
            {
                var n = ZVision.Shared.KeywordHelper.Normalize(t);
                if (n == null || !seen.Add(n)) continue;
                KeywordSuggestions.Add(KeywordVm.From(n));
                if (KeywordSuggestions.Count >= 12) break;
            }
        }
    }

    private void UpdateKeywordEmpty()
    {
        txtNoKeywords.Visibility = Keywords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void KeywordInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { AddKeywordFromInput(); e.Handled = true; }
    }

    private void AddKeyword_Click(object sender, RoutedEventArgs e) => AddKeywordFromInput();

    /// <summary>Auto-tag từ EXIF (#3): đọc metadata ảnh hiện tại -> sinh keyword phân cấp -> thêm.</summary>
    private void AutoTagExif_Click(object sender, RoutedEventArgs e)
    {
        if (_meta == null || string.IsNullOrEmpty(_currentPath)) return;
        try
        {
            var meta = ZVision.Shared.ExifReader.ReadMetadata(_currentPath);
            var tags = ZVision.Shared.ExifAutoTagger.Generate(meta);
            if (tags.Count == 0)
            {
                MessageBox.Show("Photo contains no EXIF metadata for keywords.", "Auto-tag",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            foreach (var t in tags) ApplyAddKeyword(t);
        }
        catch (Exception ex)
        {
            ZVision.Shared.AppLog.Warn("InfoPanel.AutoTagExif", $"{_currentPath}: {ex.Message}");
        }
    }

    private void AddKeywordFromInput()
    {
        var n = ZVision.Shared.KeywordHelper.Normalize(txtKeywordInput.Text);
        txtKeywordInput.Text = "";
        if (n != null) ApplyAddKeyword(n);
    }

    private void SuggestKeyword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string full)
            ApplyAddKeyword(full);
    }

    private void ApplyAddKeyword(string keyword)
    {
        if (_meta == null || string.IsNullOrEmpty(_currentPath)) return;
        var n = ZVision.Shared.KeywordHelper.Normalize(keyword);
        if (n == null) return;
        var list = ZVision.Shared.KeywordHelper.NormalizeList(_meta.Get(_currentPath).Tags);
        if (list.Any(x => string.Equals(x, n, System.StringComparison.OrdinalIgnoreCase))) return;
        list.Add(n);
        _meta.SetTags(_currentPath, list);
        _settings?.AddRecentTags(new[] { n });
        LoadKeywords(_currentPath);
    }

    private void RemoveKeyword_Click(object sender, RoutedEventArgs e)
    {
        if (_meta == null || string.IsNullOrEmpty(_currentPath)) return;
        if (sender is not FrameworkElement fe || fe.Tag is not string full) return;
        var list = ZVision.Shared.KeywordHelper.NormalizeList(_meta.Get(_currentPath).Tags);
        list.RemoveAll(x => string.Equals(x, full, System.StringComparison.OrdinalIgnoreCase));
        _meta.SetTags(_currentPath, list);
        LoadKeywords(_currentPath);
    }


    private bool _updatingCuration;
    private void UpdateCurationUi(ImageMeta? meta)
    {
        _updatingCuration = true;
        try
        {
            if (meta == null)
            {
                ratingControl.Value = 0;
                btnPick.SetResourceReference(Button.BackgroundProperty, "BgHoverBrush");
                btnReject.SetResourceReference(Button.BackgroundProperty, "BgHoverBrush");
                return;
            }
            ratingControl.Value = meta.Rating;
            if (meta.Pick == PickFlag.Pick)
                btnPick.SetResourceReference(Button.BackgroundProperty, "SuccessBrush");
            else
                btnPick.SetResourceReference(Button.BackgroundProperty, "BgHoverBrush");

            if (meta.Pick == PickFlag.Reject)
                btnReject.SetResourceReference(Button.BackgroundProperty, "DangerBrush");
            else
                btnReject.SetResourceReference(Button.BackgroundProperty, "BgHoverBrush");
        }
        finally
        {
            _updatingCuration = false;
        }
    }

    private void RatingControl_ValueChanged(object? sender, decimal e)
    {
        if (_updatingCuration || _currentPath == null || _meta == null) return;
        int rating = (int)Math.Round(e);
        _meta.SetRating(_currentPath, rating);
    }

    private void BtnResetRating_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null || _meta == null) return;
        _meta.SetRating(_currentPath, 0);
        ratingControl.Value = 0;
    }

    private void BtnPick_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null || _meta == null) return;
        var current = _meta.Get(_currentPath);
        var next = current.Pick == PickFlag.Pick ? PickFlag.None : PickFlag.Pick;
        _meta.SetPick(_currentPath, next);
        UpdateCurationUi(_meta.Get(_currentPath));
    }

    private void BtnReject_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null || _meta == null) return;
        var current = _meta.Get(_currentPath);
        var next = current.Pick == PickFlag.Reject ? PickFlag.None : PickFlag.Reject;
        _meta.SetPick(_currentPath, next);
        UpdateCurationUi(_meta.Get(_currentPath));
    }

    private void BtnUnflag_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null || _meta == null) return;
        _meta.SetPick(_currentPath, PickFlag.None);
        UpdateCurationUi(_meta.Get(_currentPath));
    }

    private void BtnLabel_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath == null || _meta == null || sender is not Button btn || btn.Tag is not string tagStr) return;
        if (Enum.TryParse<ColorLabel>(tagStr, out var label))
        {
            _meta.SetLabel(_currentPath, label);
        }
    }

    /// <summary>Gắn UI component của plugin AI Auto Tagger vào phần Keywords của InfoPanel.</summary>
    public void SetTaggerPlugin(object? uiComponent)
    {
        ccTaggerHost.Content = uiComponent;
        expAiTagger.Visibility = uiComponent != null ? Visibility.Visible : Visibility.Collapsed;
    }
}
