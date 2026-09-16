using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Wpf.Editors;
using ZeroVision.Core;
using ZeroVision.Shared;

namespace ZeroVision.Host.Workspace;

public partial class MetadataFilterBar : UserControl
{
    private IWorkspaceService? _workspace;
    private bool _suppressSelectionEvents;

    private readonly FacetColumnModel _dateCol = new() { Key = "date", Title = "Date (Year)" };
    private readonly FacetColumnModel _camCol = new() { Key = "camera", Title = "Camera" };
    private readonly FacetColumnModel _lensCol = new() { Key = "lens", Title = "Lens" };
    private readonly FacetColumnModel _isoCol = new() { Key = "iso", Title = "ISO Speed" };

    public MetadataFilterBar()
    {
        InitializeComponent();
        filterBar.Columns.Add(_dateCol);
        filterBar.Columns.Add(_camCol);
        filterBar.Columns.Add(_lensCol);
        filterBar.Columns.Add(_isoCol);
    }

    public void Bind(IWorkspaceService workspace)
    {
        _workspace = workspace;
        _workspace.FolderOpened += (_, _) => PopulateFromCurrentWorkspace();
        _workspace.ImagesRefreshed += (_, _) => PopulateFromCurrentWorkspace();
        PopulateFromCurrentWorkspace();
    }

    public void PopulateFromCurrentWorkspace()
    {
        if (_workspace == null) return;
        var images = _workspace.Images.ToList();
        if (images.Count == 0)
        {
            Dispatcher.BeginInvoke(() =>
            {
                filterBar.SummaryText = "· No photos";
                _dateCol.Items.Clear();
                _camCol.Items.Clear();
                _lensCol.Items.Clear();
                _isoCol.Items.Clear();
            });
            return;
        }

        Task.Run(() =>
        {
            var dateCounts = new Dictionary<int, int>();
            var camCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lensCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var isoCounts = new Dictionary<int, int>();

            foreach (var p in images)
            {
                var meta = ExifReader.GetOrCreate(p);
                if (meta.DateTaken.HasValue)
                {
                    int yr = meta.DateTaken.Value.Year;
                    dateCounts[yr] = dateCounts.GetValueOrDefault(yr) + 1;
                }
                string cam = meta.CameraModel ?? meta.CameraMake ?? "Unknown Camera";
                camCounts[cam] = camCounts.GetValueOrDefault(cam) + 1;

                string lens = !string.IsNullOrWhiteSpace(meta.LensModel) ? meta.LensModel : "Unknown Lens";
                lensCounts[lens] = lensCounts.GetValueOrDefault(lens) + 1;

                if (meta.Iso.HasValue && meta.Iso.Value > 0)
                {
                    int iso = meta.Iso.Value;
                    isoCounts[iso] = isoCounts.GetValueOrDefault(iso) + 1;
                }
            }

            var dateItems = new List<FacetItemModel> { new() { Key = "", DisplayText = "All Dates", Count = images.Count } };
            dateItems.AddRange(dateCounts.OrderByDescending(kv => kv.Key).Select(kv => new FacetItemModel { Key = kv.Key.ToString(), DisplayText = kv.Key.ToString(), Count = kv.Value }));

            var camItems = new List<FacetItemModel> { new() { Key = "", DisplayText = "All Cameras", Count = images.Count } };
            camItems.AddRange(camCounts.OrderByDescending(kv => kv.Value).Select(kv => new FacetItemModel { Key = kv.Key, DisplayText = kv.Key, Count = kv.Value }));

            var lensItems = new List<FacetItemModel> { new() { Key = "", DisplayText = "All Lenses", Count = images.Count } };
            lensItems.AddRange(lensCounts.OrderByDescending(kv => kv.Value).Select(kv => new FacetItemModel { Key = kv.Key, DisplayText = kv.Key, Count = kv.Value }));

            var isoItems = new List<FacetItemModel> { new() { Key = "", DisplayText = "All ISOs", Count = images.Count } };
            isoItems.AddRange(isoCounts.OrderBy(kv => kv.Key).Select(kv => new FacetItemModel { Key = kv.Key.ToString(), DisplayText = $"ISO {kv.Key}", Count = kv.Value }));

            Dispatcher.BeginInvoke(() =>
            {
                _suppressSelectionEvents = true;
                try
                {
                    filterBar.SummaryText = $"· {images.Count} photo(s)";

                    UpdateColumnItems(_dateCol, dateItems, _workspace.Filter.RequiredDateYear?.ToString());
                    UpdateColumnItems(_camCol, camItems, _workspace.Filter.RequiredCamera);
                    UpdateColumnItems(_lensCol, lensItems, _workspace.Filter.RequiredLens);
                    UpdateColumnItems(_isoCol, isoItems, _workspace.Filter.RequiredIso?.ToString());
                }
                finally
                {
                    _suppressSelectionEvents = false;
                }
            });
        });
    }

    private static void UpdateColumnItems(FacetColumnModel col, List<FacetItemModel> items, string? currentVal)
    {
        col.Items.Clear();
        foreach (var itm in items) col.Items.Add(itm);

        var selected = string.IsNullOrEmpty(currentVal)
            ? col.Items.FirstOrDefault()
            : col.Items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Key) && string.Equals(i.Key, currentVal, StringComparison.OrdinalIgnoreCase)) ?? col.Items.FirstOrDefault();

        col.SelectedItem = selected;
    }

    private void FilterBar_SelectionChanged(object? sender, (string ColumnKey, string? SelectedItemKey) e)
    {
        if (_suppressSelectionEvents || _workspace == null) return;

        switch (e.ColumnKey)
        {
            case "date":
                _workspace.Filter.RequiredDateYear = int.TryParse(e.SelectedItemKey, out int yr) && yr > 0 ? yr : null;
                break;
            case "camera":
                _workspace.Filter.RequiredCamera = string.IsNullOrEmpty(e.SelectedItemKey) ? null : e.SelectedItemKey;
                break;
            case "lens":
                _workspace.Filter.RequiredLens = string.IsNullOrEmpty(e.SelectedItemKey) ? null : e.SelectedItemKey;
                break;
            case "iso":
                _workspace.Filter.RequiredIso = int.TryParse(e.SelectedItemKey, out int iso) && iso > 0 ? iso : null;
                break;
        }

        _workspace.ApplyFilterAndSort();
    }

    private void FilterBar_FiltersCleared(object? sender, EventArgs e)
    {
        if (_workspace == null) return;
        _suppressSelectionEvents = true;
        try
        {
            _workspace.Filter.RequiredDateYear = null;
            _workspace.Filter.RequiredCamera = null;
            _workspace.Filter.RequiredLens = null;
            _workspace.Filter.RequiredIso = null;

            _dateCol.SelectedItem = _dateCol.Items.FirstOrDefault();
            _camCol.SelectedItem = _camCol.Items.FirstOrDefault();
            _lensCol.SelectedItem = _lensCol.Items.FirstOrDefault();
            _isoCol.SelectedItem = _isoCol.Items.FirstOrDefault();
        }
        finally
        {
            _suppressSelectionEvents = false;
        }
        _workspace.ApplyFilterAndSort();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Collapsed;
    }
}
