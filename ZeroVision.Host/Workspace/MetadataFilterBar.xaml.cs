using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ZeroVision.Core;
using ZeroVision.Shared;

namespace ZeroVision.Host.Workspace;

public sealed class FilterEntry<T>
{
    public string DisplayText { get; }
    public int Count { get; }
    public T? Value { get; }
    public bool IsAll { get; }
    public string CountBadge => $"({Count})";

    public FilterEntry(string text, int count, T? value, bool isAll = false)
    {
        DisplayText = text;
        Count = count;
        Value = value;
        IsAll = isAll;
    }

    public override string ToString() => DisplayText;
}

public partial class MetadataFilterBar : UserControl
{
    private IWorkspaceService? _workspace;
    private bool _suppressSelectionEvents;

    public MetadataFilterBar()
    {
        InitializeComponent();
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
                txtSummary.Text = "· Không có ảnh";
                lstDate.ItemsSource = null;
                lstCamera.ItemsSource = null;
                lstLens.ItemsSource = null;
                lstIso.ItemsSource = null;
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

            // Dựng danh sách UI
            var dateList = new List<FilterEntry<int>> { new("All Dates", images.Count, 0, true) };
            dateList.AddRange(dateCounts.OrderByDescending(kv => kv.Key).Select(kv => new FilterEntry<int>(kv.Key.ToString(), kv.Value, kv.Key)));

            var camList = new List<FilterEntry<string>> { new("All Cameras", images.Count, "", true) };
            camList.AddRange(camCounts.OrderByDescending(kv => kv.Value).Select(kv => new FilterEntry<string>(kv.Key, kv.Value, kv.Key)));

            var lensList = new List<FilterEntry<string>> { new("All Lenses", images.Count, "", true) };
            lensList.AddRange(lensCounts.OrderByDescending(kv => kv.Value).Select(kv => new FilterEntry<string>(kv.Key, kv.Value, kv.Key)));

            var isoList = new List<FilterEntry<int>> { new("All ISOs", images.Count, 0, true) };
            isoList.AddRange(isoCounts.OrderBy(kv => kv.Key).Select(kv => new FilterEntry<int>($"ISO {kv.Key}", kv.Value, kv.Key)));

            Dispatcher.BeginInvoke(() =>
            {
                _suppressSelectionEvents = true;
                try
                {
                    txtSummary.Text = $"· {images.Count} ảnh";
                    lstDate.ItemsSource = dateList;
                    lstDate.SelectedIndex = FindSelectedIndex(dateList, _workspace.Filter.RequiredDateYear);

                    lstCamera.ItemsSource = camList;
                    lstCamera.SelectedIndex = FindSelectedIndex(camList, _workspace.Filter.RequiredCamera);

                    lstLens.ItemsSource = lensList;
                    lstLens.SelectedIndex = FindSelectedIndex(lensList, _workspace.Filter.RequiredLens);

                    lstIso.ItemsSource = isoList;
                    lstIso.SelectedIndex = FindSelectedIndex(isoList, _workspace.Filter.RequiredIso);
                }
                finally
                {
                    _suppressSelectionEvents = false;
                }
            });
        });
    }

    private static int FindSelectedIndex<T>(List<FilterEntry<T>> list, object? currentVal)
    {
        if (currentVal == null) return 0; // "All"
        int idx = list.FindIndex(item => !item.IsAll && object.Equals(item.Value, currentVal));
        return idx >= 0 ? idx : 0;
    }

    private void LstDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || _workspace == null) return;
        if (lstDate.SelectedItem is FilterEntry<int> entry)
        {
            _workspace.Filter.RequiredDateYear = entry.IsAll ? null : entry.Value;
            _workspace.ApplyFilterAndSort();
        }
    }

    private void LstCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || _workspace == null) return;
        if (lstCamera.SelectedItem is FilterEntry<string> entry)
        {
            _workspace.Filter.RequiredCamera = entry.IsAll ? null : entry.Value;
            _workspace.ApplyFilterAndSort();
        }
    }

    private void LstLens_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || _workspace == null) return;
        if (lstLens.SelectedItem is FilterEntry<string> entry)
        {
            _workspace.Filter.RequiredLens = entry.IsAll ? null : entry.Value;
            _workspace.ApplyFilterAndSort();
        }
    }

    private void LstIso_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvents || _workspace == null) return;
        if (lstIso.SelectedItem is FilterEntry<int> entry)
        {
            _workspace.Filter.RequiredIso = entry.IsAll ? null : entry.Value;
            _workspace.ApplyFilterAndSort();
        }
    }

    private void BtnResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace == null) return;
        _suppressSelectionEvents = true;
        try
        {
            _workspace.Filter.RequiredDateYear = null;
            _workspace.Filter.RequiredCamera = null;
            _workspace.Filter.RequiredLens = null;
            _workspace.Filter.RequiredIso = null;

            if (lstDate.Items.Count > 0) lstDate.SelectedIndex = 0;
            if (lstCamera.Items.Count > 0) lstCamera.SelectedIndex = 0;
            if (lstLens.Items.Count > 0) lstLens.SelectedIndex = 0;
            if (lstIso.Items.Count > 0) lstIso.SelectedIndex = 0;
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
