using System.Collections.ObjectModel;

namespace ZeroVision.Core;

public interface IWorkspaceService
{
    string? CurrentFolder { get; }
    string? CurrentViewName { get; }
    ReadOnlyObservableCollection<string> Images { get; }
    ReadOnlyObservableCollection<string> Selection { get; }
    string? ActiveImage { get; }

    WorkspaceFilter Filter { get; }
    WorkspaceSort Sort { get; set; }

    void OpenFolder(string folderPath);
    void OpenCatalogView(IEnumerable<string> imagePaths, string? viewName = null);
    void SetActiveImage(string? path);
    void SetSelection(IEnumerable<string> paths);
    void AddToSelection(string path);
    void RemoveFromSelection(string path);
    void ClearSelection();
    void AddVirtualCopy(string vcPath, string originalPath);
    void RemoveVirtualCopy(string vcPath);
    void ApplyFilterAndSort();

    event EventHandler<FolderOpenedEventArgs>? FolderOpened;
    event EventHandler<ImageSelectedEventArgs>? ActiveImageChanged;
    event EventHandler<BatchSelectionChangedEventArgs>? SelectionChanged;
    event EventHandler? ImagesRefreshed;
}

public class WorkspaceFilter
{
    public int MinRating { get; set; }
    public ColorLabel? RequiredLabel { get; set; }
    public PickFlag? RequiredPick { get; set; }
    public string? Search { get; set; }

    /// <summary>Ẩn ảnh đã gắn cờ Reject (luồng cull thông dụng: loại trước, xem lại trước khi xoá).</summary>
    public bool HideRejected { get; set; }

    // Library Metadata Filter Bar
    public int? RequiredDateYear { get; set; }
    public string? RequiredCamera { get; set; }
    public string? RequiredLens { get; set; }
    public int? RequiredIso { get; set; }
}

public enum WorkspaceSort { NameAsc, NameDesc, DateAsc, DateDesc, SizeAsc, SizeDesc, RatingDesc }

public class FolderOpenedEventArgs : EventArgs
{
    public string FolderPath { get; }
    public IReadOnlyList<string> Images { get; }
    public FolderOpenedEventArgs(string folderPath, IReadOnlyList<string> images)
    {
        FolderPath = folderPath;
        Images = images;
    }
}

public class ImageSelectedEventArgs : EventArgs
{
    public string? PreviousPath { get; }
    public string? CurrentPath { get; }
    public ImageSelectedEventArgs(string? previousPath, string? currentPath)
    {
        PreviousPath = previousPath;
        CurrentPath = currentPath;
    }
}

public class BatchSelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<string> Selection { get; }
    public BatchSelectionChangedEventArgs(IReadOnlyList<string> selection)
    {
        Selection = selection;
    }
}
