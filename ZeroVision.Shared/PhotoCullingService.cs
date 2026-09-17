using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Curation;
using ZeroVision.Core;

namespace ZeroVision.Shared;

/// <summary>
/// Implementation of <see cref="IPhotoCullingService"/> leveraging ZeroGraphics.Vision curation algorithms.
/// Evaluates blur, highlight clipping, extreme exposure, and burst duplicates to automate Lightroom Pick/Reject curation.
/// </summary>
public class PhotoCullingService : IPhotoCullingService
{
    private readonly IImageMetaService _metaService;
    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".arw", ".dng", ".raf", ".rw2", ".orf", ".pef", ".srw"
    };

    public PhotoCullingService(IImageMetaService metaService)
    {
        _metaService = metaService ?? throw new ArgumentNullException(nameof(metaService));
    }

    public async Task<PhotoCullSummary> AnalyzePhotosAsync(
        IReadOnlyList<string> imagePaths,
        IProgress<(int current, int total, string currentFile)>? progress = null,
        CancellationToken ct = default)
    {
        if (imagePaths == null || imagePaths.Count == 0)
            return new PhotoCullSummary();

        return await Task.Run(() =>
        {
            var candidates = new List<CurationCandidate>();
            var qualityMap = new Dictionary<string, QualityAssessment>(StringComparer.OrdinalIgnoreCase);

            int total = imagePaths.Count;
            int current = 0;

            foreach (var path in imagePaths)
            {
                ct.ThrowIfCancellationRequested();
                current++;
                progress?.Report((current, total, Path.GetFileName(path)));

                try
                {
                    using var buffer = LoadImageBuffer(path);
                    if (buffer == null) continue;

                    ulong hash = DifferenceHash.Compute(buffer);
                    var sharpness = ImageSharpnessEvaluator.Evaluate(buffer);
                    var exposure = ImageExposureEvaluator.Evaluate(buffer);
                    var quality = QualityScorer.Score(sharpness, exposure);

                    DateTime? captureTime = null;
                    try
                    {
                        captureTime = File.GetLastWriteTimeUtc(path);
                    }
                    catch { }

                    var candidate = new CurationCandidate
                    {
                        Id = path,
                        Hash = hash,
                        EffectiveSharpness = sharpness.EffectiveSharpness,
                        ExposureScore = exposure.ExposureScore,
                        CaptureTime = captureTime,
                        Tag = quality
                    };

                    candidates.Add(candidate);
                    qualityMap[path] = quality;
                }
                catch (Exception ex)
                {
                    AppLog.Warn("PhotoCulling.Analyze", $"Failed to analyze {path}: {ex.Message}");
                }
            }

            ct.ThrowIfCancellationRequested();

            // Run burst & duplicate clustering
            var clusterOptions = new BurstClusterOptions
            {
                MaxHammingDistance = 6,
                MaxTimeDeltaSeconds = 10.0
            };
            var clusters = BurstClusterer.Cluster(candidates, clusterOptions);

            var items = new List<PhotoCullItem>();
            var clusterLookup = new Dictionary<string, (BurstCluster Cluster, bool IsBest)>(StringComparer.OrdinalIgnoreCase);

            foreach (var cluster in clusters)
            {
                if (cluster.Members.Count > 1)
                {
                    if (cluster.BestShot != null)
                    {
                        clusterLookup[cluster.BestShot.Id] = (cluster, true);
                    }
                    foreach (var red in cluster.RedundantShots)
                    {
                        clusterLookup[red.Id] = (cluster, false);
                    }
                }
            }

            foreach (var candidate in candidates)
            {
                var quality = qualityMap[candidate.Id];
                var item = new PhotoCullItem
                {
                    ImagePath = candidate.Id,
                    StarRating = quality.StarRating,
                    TqiScore = quality.Tqi,
                    EffectiveSharpness = quality.Sharpness.EffectiveSharpness,
                    ExposureScore = quality.Exposure.ExposureScore,
                    IsSevereBlur = quality.Sharpness.IsSevereBlur,
                    IsBlownHighlights = quality.Exposure.IsBlownHighlights,
                    IsCrushedShadows = quality.Exposure.IsCrushedShadows,
                    Reasons = new List<string>(quality.Reasons)
                };

                // Check if candidate belongs to a burst/duplicate cluster
                if (clusterLookup.TryGetValue(candidate.Id, out var clusterInfo))
                {
                    item.BurstClusterId = clusterInfo.Cluster.ClusterId;
                    item.IsBestOfBurst = clusterInfo.IsBest;

                    if (clusterInfo.IsBest)
                    {
                        // Best shot in burst sequence
                        item.ProposedFlag = quality.Tqi >= 55.0 ? PickFlag.Pick : PickFlag.None;
                        item.Reasons.Insert(0, $"Best shot in burst #{clusterInfo.Cluster.ClusterId} ({clusterInfo.Cluster.Members.Count} shots)");
                    }
                    else
                    {
                        // Redundant shot in burst
                        item.IsDuplicateDiscard = true;
                        item.ProposedFlag = PickFlag.Reject;
                        string bestName = clusterInfo.Cluster.BestShot != null ? Path.GetFileName(clusterInfo.Cluster.BestShot.Id) : "leader";
                        item.Reasons.Insert(0, $"Redundant duplicate in burst #{clusterInfo.Cluster.ClusterId} (Best: {bestName})");
                    }
                }
                else
                {
                    // Standalone image: follow quality recommendations
                    item.ProposedFlag = quality.RecommendedAction switch
                    {
                        CurationAction.Reject => PickFlag.Reject,
                        CurationAction.Pick => PickFlag.Pick,
                        _ => PickFlag.None
                    };
                }

                items.Add(item);
            }

            var summary = new PhotoCullSummary
            {
                TotalAnalyzed = items.Count,
                RecommendedRejections = items.Count(i => i.ProposedFlag == PickFlag.Reject),
                RecommendedPicks = items.Count(i => i.ProposedFlag == PickFlag.Pick),
                DuplicateClustersCount = clusters.Count(c => c.Members.Count > 1),
                BlurryCount = items.Count(i => i.IsSevereBlur),
                BlownExposureCount = items.Count(i => i.IsBlownHighlights || i.IsCrushedShadows),
                Items = items
            };

            return summary;
        }, ct);
    }

    public async Task ApplyCullingFlagsAsync(
        IEnumerable<PhotoCullItem> items,
        bool applyRejections = true,
        bool applyPicks = true,
        bool applyRatings = true)
    {
        if (items == null) return;

        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (applyRejections && item.ProposedFlag == PickFlag.Reject)
                {
                    _metaService.SetPick(item.ImagePath, PickFlag.Reject);
                }
                else if (applyPicks && item.ProposedFlag == PickFlag.Pick)
                {
                    _metaService.SetPick(item.ImagePath, PickFlag.Pick);
                }

                if (applyRatings && item.StarRating > 0)
                {
                    _metaService.SetRating(item.ImagePath, item.StarRating);
                }
            }
        });
    }

    private static unsafe ImageBuffer? LoadImageBuffer(string path, int maxDimension = 1920)
    {
        var ext = Path.GetExtension(path);
        Image<Bgra32>? image = null;

        try
        {
            // For RAW files, try embedded high-res JPEG first (fastest, zero demosaic overhead)
            if (RawExtensions.Contains(ext) && File.Exists(path))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(path);
                    var jpegLocation = ZeroVision.Imaging.RawPreviewExtractor.FindLargestJpeg(fileBytes);
                    if (jpegLocation.HasValue)
                    {
                        using var ms = new MemoryStream(fileBytes, jpegLocation.Value.Offset, jpegLocation.Value.Length, false);
                        image = Image.Load<Bgra32>(ms);
                    }
                }
                catch
                {
                    image = null;
                }
            }

            // Fallback to standard ImageSharp decode
            image ??= Image.Load<Bgra32>(path);

            // Downsample to maxDimension if image is massive (e.g. 45MP -> 1080p-1440p)
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(maxDimension, maxDimension),
                    Mode = ResizeMode.Max
                }));
            }

            int w = image.Width;
            int h = image.Height;
            var buffer = new ImageBuffer(w, h, ImageFormatMode.Bgra32);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var rowSpan = accessor.GetRowSpan(y);
                    byte* pDst = buffer.GetRowPointer(y);
                    fixed (Bgra32* pSrc = rowSpan)
                    {
                        Buffer.MemoryCopy(pSrc, pDst, (long)w * 4, (long)w * 4);
                    }
                }
            });

            return buffer;
        }
        finally
        {
            image?.Dispose();
        }
    }
}
