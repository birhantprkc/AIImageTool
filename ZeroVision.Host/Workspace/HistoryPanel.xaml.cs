using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Wpf.Editors;
using ZeroVision.Core;

namespace ZeroVision.Host.Workspace;

public partial class HistoryPanel : UserControl
{
    private IWorkspaceService? _workspace;
    private IHistoryService? _history;
    private DevelopRenderer? _renderer;

    public HistoryPanel()
    {
        InitializeComponent();
    }

    public void Bind(IWorkspaceService workspace, IHistoryService history, DevelopRenderer? renderer = null)
    {
        _workspace = workspace;
        _history = history;
        _renderer = renderer;
        _workspace.ActiveImageChanged += (s, e) => Refresh(e.CurrentPath);
        _history.HistoryChanged += (s, e) =>
        {
            if (string.Equals(e.ImagePath, _workspace.ActiveImage, StringComparison.OrdinalIgnoreCase))
                Refresh(_workspace.ActiveImage);
        };
    }

    private void Refresh(string? path)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ctrlHistory.Steps.Clear();
            ctrlHistory.Snapshots.Clear();
            if (_history == null || string.IsNullOrEmpty(path)) return;

            var stack = _history.GetStack(path);
            int ptr = _history.GetPointer(path);

            ctrlHistory.Steps.Add(new HistoryTimelineItemModel
            {
                Index = 0,
                Title = "Original",
                IsActive = ptr == 0,
                IsFuture = false,
                TimeText = ""
            });

            for (int i = 0; i < stack.Count; i++)
            {
                var op = stack[i];
                var future = (i + 1) > ptr;
                ctrlHistory.Steps.Add(new HistoryTimelineItemModel
                {
                    Index = i + 1,
                    Title = ZeroVision.Shared.OpDisplayNames.Get(op.OpType, op.Title),
                    IsActive = (i + 1) == ptr,
                    IsFuture = future,
                    TimeText = op.Timestamp.ToLocalTime().ToString("HH:mm")
                });
            }

            ctrlHistory.CurrentIndex = ptr;

            foreach (var s in _history.GetSnapshots(path))
            {
                ctrlHistory.Snapshots.Add(new HistorySnapshotItemModel
                {
                    Name = s.Name,
                    TimeText = s.CreatedAt.ToLocalTime().ToString("dd/MM HH:mm")
                });
            }

            RenderThumbnails(path, stack);
        });
    }

    /// <summary>Render small thumbnail for each history step off-UI then bind to step.</summary>
    private async void RenderThumbnails(string path, IReadOnlyList<EditOperation> stack)
    {
        if (_renderer == null || !_renderer.CanDecode(path)) return;
        var ops = new List<EditOperation>(stack);
        var stepsSnapshot = ctrlHistory.Steps.ToList();
        for (int i = 0; i < stepsSnapshot.Count; i++)
        {
            var step = stepsSnapshot[i];
            int pointer = step.Index;
            var bmp = await _renderer.RenderThumbnailAsync(path, ops, pointer, 44);
            if (bmp != null && ctrlHistory.Steps.Contains(step)) step.Thumbnail = bmp;
        }
    }

    private void CtrlHistory_StepSelected(object? sender, int index)
    {
        if (_workspace?.ActiveImage != null) _history?.SetPointer(_workspace.ActiveImage, index);
    }

    private void CtrlHistory_UndoRequested(object? sender, EventArgs e)
    {
        if (_workspace?.ActiveImage != null) _history?.Undo(_workspace.ActiveImage);
    }

    private void CtrlHistory_RedoRequested(object? sender, EventArgs e)
    {
        if (_workspace?.ActiveImage != null) _history?.Redo(_workspace.ActiveImage);
    }

    private void CtrlHistory_ClearRequested(object? sender, EventArgs e)
    {
        if (_workspace?.ActiveImage != null) _history?.Clear(_workspace.ActiveImage);
    }

    private void CtrlHistory_SnapshotSaved(object? sender, EventArgs e)
    {
        if (_history == null || _workspace?.ActiveImage == null)
        {
            MessageBox.Show("Please select a photo before saving snapshot.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        int n = _history.GetSnapshots(_workspace.ActiveImage).Count + 1;
        var dlg = new InputDialog("Save Snapshot", "Snapshot name:", $"Snapshot {n}");
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            _history.SaveSnapshot(_workspace.ActiveImage, dlg.Result.Trim());
    }

    private void CtrlHistory_SnapshotSelected(object? sender, string name)
    {
        if (_history == null || _workspace?.ActiveImage == null) return;
        _history.ApplySnapshot(_workspace.ActiveImage, name);
    }

    private void CtrlHistory_SnapshotDeleted(object? sender, string name)
    {
        if (_history == null || _workspace?.ActiveImage == null) return;
        _history.DeleteSnapshot(_workspace.ActiveImage, name);
    }
}
