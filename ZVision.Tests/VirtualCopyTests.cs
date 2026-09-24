using System;
using System.IO;
using System.Linq;
using Xunit;
using ZVision.Core;
using ZVision.Imaging;
using ZVision.Shared;

namespace ZVision.Tests;

public class VirtualCopyTests : IDisposable
{
    private readonly string _dir;
    private readonly string _imgPath;

    public VirtualCopyTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "zv_vc_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        _imgPath = Path.Combine(_dir, "DSC_0001.JPG");
        File.WriteAllText(_imgPath, "dummy image content");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void VirtualCopyHelper_ResolveDiskPath_ExtractsCorrectPath()
    {
        string original = @"C:\Photos\Landscape.jpg";
        string vc1 = @"C:\Photos\Landscape.jpg#vc1";
        string vc2 = @"C:\Photos\Landscape.jpg#vc2";

        Assert.Equal(original, VirtualCopyHelper.ResolveDiskPath(original));
        Assert.Equal(original, VirtualCopyHelper.ResolveDiskPath(vc1));
        Assert.Equal(original, VirtualCopyHelper.ResolveDiskPath(vc2));
        Assert.Equal("1", VirtualCopyHelper.GetVirtualCopyNumber(vc1));
        Assert.Equal("2", VirtualCopyHelper.GetVirtualCopyNumber(vc2));
        Assert.True(VirtualCopyHelper.IsVirtualCopy(vc1));
        Assert.False(VirtualCopyHelper.IsVirtualCopy(original));
    }

    [Fact]
    public void WorkspaceService_AddVirtualCopy_InsertsAndActivates()
    {
        var ws = new WorkspaceService();
        var history = new HistoryService();
        history.Push(_imgPath, new EditOperation { PluginId = "Develop", OpType = "Exposure", Params = new() { ["ev"] = "1.0" } });

        ws.OpenFolder(_dir);
        // Wait or ensure images loaded
        ws.ApplyFilterAndSort();

        string vcPath = history.CreateVirtualCopy(_imgPath);
        Assert.Contains(HistoryService.VirtualCopySuffix, vcPath);

        ws.AddVirtualCopy(vcPath, _imgPath);

        Assert.Contains(vcPath, ws.Images);
        Assert.Equal(vcPath, ws.ActiveImage);
        Assert.Contains(vcPath, ws.Selection);

        // Virtual copy stack matches cloned operations
        var vcStack = history.GetStack(vcPath);
        Assert.Single(vcStack);
        Assert.Equal("Exposure", vcStack[0].OpType);
    }

    [Fact]
    public void WorkspaceService_RemoveVirtualCopy_RemovesWithoutDeletingDiskFile()
    {
        var ws = new WorkspaceService();
        var history = new HistoryService();
        string vcPath = history.CreateVirtualCopy(_imgPath);

        ws.AddVirtualCopy(vcPath, _imgPath);
        Assert.Contains(vcPath, ws.Images);

        // Delete virtual copy
        bool deleted = history.DeleteVirtualCopy(vcPath);
        Assert.True(deleted);

        ws.RemoveVirtualCopy(vcPath);
        Assert.DoesNotContain(vcPath, ws.Images);

        // Original file on disk must STILL exist intact
        Assert.True(File.Exists(_imgPath));
    }

    [Fact]
    public void ImageDecoderRegistry_CanDecode_ResolvesVirtualCopy()
    {
        var reg = ImageDecoderRegistry.CreateDefault();
        string vc = _imgPath + "#vc1";

        Assert.True(reg.CanDecode(_imgPath));
        Assert.True(reg.CanDecode(vc));
    }
}
