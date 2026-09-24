using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ZVision.Core;
using ZVision.Shared;

namespace ZVision.Tests;

public class PhotoCullingServiceTests
{
    private static string CreateTempTestDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zerovision_cull_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task AnalyzePhotosAsync_IdentifiesBlurBlownAndBurstDuplicates()
    {
        var dir = CreateTempTestDir();
        try
        {
            // 1. Create a uniform blurry/flat image
            var blurryPath = Path.Combine(dir, "photo_blurry.png");
            using (var img = new Image<Rgba32>(100, 100))
            {
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            row[x] = new Rgba32(128, 128, 128, 255);
                        }
                    }
                });
                img.SaveAsPng(blurryPath);
            }

            // 2. Create a blown highlights image (20% pinned to 255)
            var blownPath = Path.Combine(dir, "photo_blown.png");
            using (var img = new Image<Rgba32>(100, 100))
            {
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            byte val = x < 25 ? (byte)255 : (byte)120;
                            row[x] = new Rgba32(val, val, val, 255);
                        }
                    }
                });
                img.SaveAsPng(blownPath);
            }

            // 3. Create a high-contrast sharp image
            var sharp1Path = Path.Combine(dir, "photo_sharp1.png");
            using (var img = new Image<Rgba32>(100, 100))
            {
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            byte val = ((x / 4) % 2 == (y / 4) % 2) ? (byte)240 : (byte)15;
                            row[x] = new Rgba32(val, val, val, 255);
                        }
                    }
                });
                img.SaveAsPng(sharp1Path);
            }

            // 4. Create a near-duplicate burst shot with slight noise
            var sharp2Path = Path.Combine(dir, "photo_sharp2.png");
            using (var img = new Image<Rgba32>(100, 100))
            {
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            byte val = ((x / 4) % 2 == (y / 4) % 2) ? (byte)235 : (byte)20;
                            row[x] = new Rgba32(val, val, val, 255);
                        }
                    }
                });
                img.SaveAsPng(sharp2Path);
            }

            var metaService = new ImageMetaService();
            var cullingService = new PhotoCullingService(metaService);

            var summary = await cullingService.AnalyzePhotosAsync(new[]
            {
                blurryPath, blownPath, sharp1Path, sharp2Path
            });

            Assert.Equal(4, summary.TotalAnalyzed);

            // Blurry image must be flagged reject
            var blurryItem = summary.Items.Find(i => i.ImagePath == blurryPath);
            Assert.NotNull(blurryItem);
            Assert.True(blurryItem!.IsSevereBlur);
            Assert.Equal(PickFlag.Reject, blurryItem.ProposedFlag);

            // Blown image must be flagged reject
            var blownItem = summary.Items.Find(i => i.ImagePath == blownPath);
            Assert.NotNull(blownItem);
            Assert.True(blownItem!.IsBlownHighlights);
            Assert.Equal(PickFlag.Reject, blownItem.ProposedFlag);

            // Sharp1 and Sharp2 should be clustered together in a burst
            var sharp1Item = summary.Items.Find(i => i.ImagePath == sharp1Path);
            var sharp2Item = summary.Items.Find(i => i.ImagePath == sharp2Path);
            Assert.NotNull(sharp1Item);
            Assert.NotNull(sharp2Item);
            Assert.Equal(sharp1Item!.BurstClusterId, sharp2Item!.BurstClusterId);
            Assert.NotNull(sharp1Item.BurstClusterId);

            // One should be best (Pick), one should be redundant duplicate (Reject)
            Assert.True(
                (sharp1Item.IsBestOfBurst && sharp2Item.IsDuplicateDiscard) ||
                (sharp2Item.IsBestOfBurst && sharp1Item.IsDuplicateDiscard)
            );

            // Test applying flags
            await cullingService.ApplyCullingFlagsAsync(summary.Items);

            Assert.Equal(PickFlag.Reject, metaService.Get(blurryPath).Pick);
            Assert.Equal(PickFlag.Reject, metaService.Get(blownPath).Pick);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
