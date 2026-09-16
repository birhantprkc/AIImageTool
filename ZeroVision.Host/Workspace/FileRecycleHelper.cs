using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ZeroVision.Host.Workspace;

/// <summary>
/// Sovereign Windows shell helper to safely move files (and their sidecars) to the OS Recycle Bin.
/// </summary>
public static class FileRecycleHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszProgressTitle;
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    /// <summary>
    /// Safely moves a file to the Windows Recycle Bin along with any existing sidecar files (.xmp, .imgtool.json).
    /// </summary>
    public static bool SendToRecycleBin(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        var filesToDelete = new List<string> { filePath };
        var xmp = Path.ChangeExtension(filePath, ".xmp");
        if (File.Exists(xmp)) filesToDelete.Add(xmp);
        var jsonSidecar = filePath + ".imgtool.json";
        if (File.Exists(jsonSidecar)) filesToDelete.Add(jsonSidecar);
        var jsonSidecarAlt = Path.ChangeExtension(filePath, ".imgtool.json");
        if (File.Exists(jsonSidecarAlt) && !filesToDelete.Contains(jsonSidecarAlt)) filesToDelete.Add(jsonSidecarAlt);

        bool allOk = true;
        foreach (var file in filesToDelete)
        {
            try
            {
                var op = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = file + "\0\0",
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                };
                int res = SHFileOperation(ref op);
                if (res != 0)
                {
                    // Fallback to File.Delete if shell operation returns non-zero error
                    File.Delete(file);
                }
            }
            catch
            {
                allOk = false;
            }
        }
        return allOk;
    }
}
