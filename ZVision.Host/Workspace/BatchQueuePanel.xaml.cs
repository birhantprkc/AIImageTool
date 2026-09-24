using System.Linq;
using System.Windows.Controls;
using ZeroUI.Wpf.Editors;
using ZVision.Core;

namespace ZVision.Host.Workspace;

public partial class BatchQueuePanel : UserControl
{
    private IBatchService? _batch;

    public BatchQueuePanel()
    {
        InitializeComponent();

        queueControl.MaxParallelChanged += (s, n) =>
        {
            if (_batch != null) _batch.MaxParallel = n;
        };

        queueControl.PauseResumeClicked += (s, isPaused) =>
        {
            if (_batch == null) return;
            if (isPaused) _batch.Pause(); else _batch.Resume();
        };

        queueControl.ClearCompletedClicked += (s, e) => _batch?.ClearCompleted();

        queueControl.RetryTaskClicked += (s, task) =>
        {
            if (!string.IsNullOrEmpty(task?.Id)) _batch?.RetryJob(task.Id);
        };

        queueControl.RemoveTaskClicked += (s, task) =>
        {
            if (!string.IsNullOrEmpty(task?.Id)) _batch?.RemoveJob(task.Id);
        };
    }

    public void Bind(IBatchService batch)
    {
        _batch = batch;
        queueControl.MaxParallel = _batch.MaxParallel;
        queueControl.IsPaused = _batch.IsPaused;
        _batch.QueueChanged += (s, e) => Refresh();
        _batch.JobUpdated += (s, j) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_batch == null) return;
            queueControl.IsPaused = _batch.IsPaused;
            var src = _batch.Jobs.ToList();
            var tasks = queueControl.Tasks;

            for (int i = 0; i < src.Count; i++)
            {
                var j = src[i];
                if (i < tasks.Count)
                {
                    var item = tasks[i];
                    item.Id = j.Id;
                    item.Update(j.DisplayName, j.Progress, MapStatus(j.Status), j.Error);
                }
                else
                {
                    var item = new BatchTaskItemModel(j.Id, j.DisplayName);
                    item.Update(j.DisplayName, j.Progress, MapStatus(j.Status), j.Error);
                    tasks.Add(item);
                }
            }
            while (tasks.Count > src.Count) tasks.RemoveAt(tasks.Count - 1);
        });
    }

    private static BatchTaskStatus MapStatus(BatchJobStatus s) => s switch
    {
        BatchJobStatus.Running => BatchTaskStatus.Running,
        BatchJobStatus.Completed => BatchTaskStatus.Completed,
        BatchJobStatus.Failed => BatchTaskStatus.Failed,
        BatchJobStatus.Canceled => BatchTaskStatus.Canceled,
        BatchJobStatus.Paused => BatchTaskStatus.Paused,
        _ => BatchTaskStatus.Pending
    };
}
