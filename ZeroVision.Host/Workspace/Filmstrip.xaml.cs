using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Editors;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

public partial class Filmstrip : UserControl
{
    private IWorkspaceService? _workspace;
    private IThumbnailService? _thumbs;
    private IImageMetaService? _meta;
    private IHistoryService? _history;
    private DevelopClipboard? _clipboard;

    public Filmstrip()
    {
        InitializeComponent();
    }

    public void Bind(IWorkspaceService workspace, IThumbnailService thumbs, IImageMetaService meta)
    {
        _workspace = workspace;
        _thumbs = thumbs;
        _meta = meta;
        _workspace.FolderOpened += OnFolderOpened;
        _workspace.ActiveImageChanged += OnActiveChanged;
        _workspace.SelectionChanged += OnSelectionChanged;
        _thumbs.ThumbnailReady += OnThumbReady;
        _meta.MetaChanged += OnMetaChanged;
    }

    /// <summary>Provide service for context menu (call after Bind).</summary>
    public void BindContext(IHistoryService history, DevelopClipboard clipboard)
    {
        _history = history;
        _clipboard = clipboard;
    }

    private void OnFolderOpened(object? sender, FolderOpenedEventArgs e)
    {
        var paths = e.Images.ToList();
        var meta = _meta;
        var thumbs = _thumbs;
        Task.Run(() =>
        {
            var list = new List<FilmstripItemModel>(paths.Count);
            foreach (var p in paths)
            {
                var item = new FilmstripItemModel
                {
                    Id = p,
                    Title = Path.GetFileName(p)
                };
                if (meta != null) ApplyMeta(item, meta.Get(p));
                var cached = thumbs?.TryGetThumbnailPath(p, 128);
                if (cached != null) item.Thumbnail = LoadBitmap(cached);
                list.Add(item);
            }
            Dispatcher.BeginInvoke(() =>
            {
                ctrlFilmstrip.Items.Clear();
                foreach (var itm in list) ctrlFilmstrip.Items.Add(itm);
            });
        });
    }

    private void OnThumbReady(object? sender, ThumbnailReadyEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var t in ctrlFilmstrip.Items)
            {
                if (string.Equals(t.Id, e.ImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    t.Thumbnail = LoadBitmap(e.ThumbnailPath);
                    break;
                }
            }
        });
    }

    private void OnMetaChanged(object? sender, ImageMetaChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var t in ctrlFilmstrip.Items)
            {
                if (string.Equals(t.Id, e.ImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMeta(t, e.Meta);
                    break;
                }
            }
        });
    }

    private void OnActiveChanged(object? sender, ImageSelectedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            FilmstripItemModel? hit = null;
            foreach (var t in ctrlFilmstrip.Items)
            {
                t.IsActive = string.Equals(t.Id, e.CurrentPath, StringComparison.OrdinalIgnoreCase);
                if (t.IsActive) hit = t;
            }
            if (hit != null) ctrlFilmstrip.ScrollIntoView(hit);
        });
    }

    private void OnSelectionChanged(object? sender, BatchSelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var set = new HashSet<string>(e.Selection, StringComparer.OrdinalIgnoreCase);
            foreach (var t in ctrlFilmstrip.Items)
            {
                t.IsSelected = set.Contains(t.Id);
            }
        });
    }

    private void CtrlFilmstrip_ItemClicked(object? sender, FilmstripItemClickEventArgs e)
    {
        if (_workspace == null) return;
        if (e.IsControlDown)
        {
            if (_workspace.Selection.Contains(e.Item.Id)) _workspace.RemoveFromSelection(e.Item.Id);
            else _workspace.AddToSelection(e.Item.Id);
        }
        else
        {
            _workspace.SetSelection(new[] { e.Item.Id });
        }
        _workspace.SetActiveImage(e.Item.Id);
    }

    private void CtrlFilmstrip_ItemRightClicked(object? sender, FilmstripItemModel item)
    {
        if (_workspace != null && _meta != null && _history != null && _clipboard != null)
        {
            if (!_workspace.Selection.Contains(item.Id))
            {
                _workspace.SetSelection(new[] { item.Id });
                _workspace.SetActiveImage(item.Id);
            }
            var menu = ImageContextMenu.Build(item.Id, _workspace, _meta, _history, _clipboard);
            menu.IsOpen = true;
        }
    }

    private static void ApplyMeta(FilmstripItemModel item, ImageMeta meta)
    {
        item.Rating = meta.Rating;
        item.LabelBrush = meta.Label switch
        {
            ColorLabel.Red => Brushes.Red,
            ColorLabel.Yellow => Brushes.Gold,
            ColorLabel.Green => Brushes.LimeGreen,
            ColorLabel.Blue => Brushes.DodgerBlue,
            ColorLabel.Purple => Brushes.MediumPurple,
            _ => null
        };
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
