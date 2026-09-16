using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using ZeroVision.Shared;

namespace ZeroVision.Host.Workspace;

// Grid stacking UI (8.7): group consecutive burst photos into stacks, showing cover + count badge.
public partial class CenterPreview
{
    private bool _stacked;
    private List<ThumbItem>? _allGridBackup; // full item list before stacking (to unstack)

    private void BtnStack_Click(object sender, RoutedEventArgs e) => ToggleStacking();

    private void ToggleStacking()
    {
        if (_stacked)
        {
            // unstack: restore full list.
            if (_allGridBackup != null)
            {
                foreach (var it in _allGridBackup) it.StackCount = 0;
                GridItems = new ObservableCollection<ThumbItem>(_allGridBackup);
                icGrid.ItemsSource = GridItems;
                _allGridBackup = null;
            }
            _stacked = false;
            btnStack.Background = ThemeManager.GetBrush("BgHoverBrush");
            return;
        }

        var current = GridItems.ToList();
        if (current.Count == 0) return;
        _allGridBackup = current;

        // group by file modification time (approximates capture time when fast EXIF is unavailable).
        var byPath = current.ToDictionary(t => t.ImagePath, StringComparer.OrdinalIgnoreCase);
        var timed = current.Select(t =>
        {
            DateTime when;
            try { when = File.GetLastWriteTime(t.ImagePath); } catch { when = DateTime.MinValue; }
            return (t.ImagePath, when);
        });

        var stacks = ImageStacker.StackByTime(timed, thresholdSeconds: 3.0);
        var covers = new List<ThumbItem>();
        foreach (var st in stacks)
        {
            if (st.Items.Count == 0) continue;
            if (!byPath.TryGetValue(st.Cover, out var cover)) continue;
            cover.StackCount = st.Items.Count;
            covers.Add(cover);
        }

        GridItems = new ObservableCollection<ThumbItem>(covers);
        icGrid.ItemsSource = GridItems;
        _stacked = true;
        btnStack.Background = ThemeManager.GetBrush("AccentBrush");
        SetMode(LighttableMode.Grid);
    }
}
