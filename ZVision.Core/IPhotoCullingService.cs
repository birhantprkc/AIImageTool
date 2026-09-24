using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZVision.Core;

/// <summary>
/// Result of intelligent photo culling analysis for an individual image.
/// </summary>
public class PhotoCullItem
{
    public string ImagePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(ImagePath);
    public PickFlag ProposedFlag { get; set; } = PickFlag.None;
    public int StarRating { get; set; } = 3;
    public double TqiScore { get; set; }
    public double EffectiveSharpness { get; set; }
    public double ExposureScore { get; set; }
    public int? BurstClusterId { get; set; }
    public bool IsBestOfBurst { get; set; }
    public bool IsSevereBlur { get; set; }
    public bool IsBlownHighlights { get; set; }
    public bool IsCrushedShadows { get; set; }
    public bool IsDuplicateDiscard { get; set; }
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Aggregate summary of a culling run across a folder or selection.
/// </summary>
public class PhotoCullSummary
{
    public int TotalAnalyzed { get; set; }
    public int RecommendedRejections { get; set; }
    public int RecommendedPicks { get; set; }
    public int DuplicateClustersCount { get; set; }
    public int BlurryCount { get; set; }
    public int BlownExposureCount { get; set; }
    public List<PhotoCullItem> Items { get; set; } = new();
}

/// <summary>
/// Intelligent photo culling and quality evaluation service.
/// Identifies severe blur, blown highlights/crushed exposure, and burst duplicates to propose Lightroom Pick/Reject flags.
/// </summary>
public interface IPhotoCullingService
{
    Task<PhotoCullSummary> AnalyzePhotosAsync(
        IReadOnlyList<string> imagePaths,
        IProgress<(int current, int total, string currentFile)>? progress = null,
        CancellationToken ct = default);

    Task ApplyCullingFlagsAsync(
        IEnumerable<PhotoCullItem> items,
        bool applyRejections = true,
        bool applyPicks = true,
        bool applyRatings = true);
}
