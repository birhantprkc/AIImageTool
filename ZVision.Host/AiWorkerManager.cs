using System;
using System.Diagnostics;
using System.IO;

namespace ZVision.Host;

public class AiWorkerManager : IDisposable
{
    private Process? _pythonProcess;

    public void StartWorker()
    {
        try
        {
            // Mô hình tìm kiếm: 1. Tìm bản Publish (AuraAI) -> 2. Tự kiếm Source Dev gốc
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string workerDir = Path.Combine(baseDir, "AuraAI");
            string scriptPath = Path.Combine(workerDir, "main.py");
            string pythonExe = Path.Combine(workerDir, "python.exe");

            // Nếu không ở trong môi trường Publish Production thì fallback về Source gốc (Developer)
            if (!Directory.Exists(workerDir) || !File.Exists(scriptPath))
            {
                // Thư viện thường nằm cách Host/bin/Debug/net8/win-x64 => giật cấp về lại root
                var parent = Directory.GetParent(baseDir);
                while (parent != null && parent.Name != "ZVision") { parent = parent.Parent; }
                
                if (parent != null)
                {
                    workerDir = Path.Combine(parent.FullName, "ZVision.Worker.AuraSR");
                    scriptPath = Path.Combine(workerDir, "main.py");
                    pythonExe = "python"; // Dùng biến môi trường python hệ thống
                }
            }

            if (!File.Exists(scriptPath))
            {
                File.AppendAllText("worker.log", $"[{DateTime.Now}] AI Worker Disabled (Missing main.py) at: {scriptPath}\n");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(pythonExe) ? pythonExe : "python", // Tự động xài Python hệ thống nếu không có Portable
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = workerDir,
                UseShellExecute = false,
                CreateNoWindow = true,     // TÀNG HÌNH VỚI NGƯỜI DÙNG
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _pythonProcess = Process.Start(startInfo);
            
            // Ghi nhật ký Console của Python âm thầm vào File tránh bị treo (Deadlock Buffer)
            if (_pythonProcess != null)
            {
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();
                
                _pythonProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) File.AppendAllText("aura_stdout.log", e.Data + "\n"); };
                _pythonProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) File.AppendAllText("aura_stderr.log", e.Data + "\n"); };
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText("worker.log", $"[{DateTime.Now}] Error activating background server:\n{ex}\n");
        }
    }

    public void Dispose()
    {
        if (_pythonProcess != null && !_pythonProcess.HasExited)
        {
            try
            {
                _pythonProcess.Kill(true); // Kill toàn bộ cây quy trình (Tắt Cả Uvicorn)
                _pythonProcess.WaitForExit(2000);
            }
            catch { }
        }
    }
}
