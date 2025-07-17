using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace MpvWatchdogApp
{
    class MpvWatchdog
    {
        private static string mpvExecutable = "mpv.exe";
        private static string pipeName = @"\\.\pipe\mpv_control_pipe";
        private static string startupPlaylistPath = @"c:\data\VideoKiosk\01.txt";
        private static string mpvLogPath = @"c:\data\VideoKiosk\mpv.log";
        private static string mpvArguments = @$"--audio=no --border=no --title-bar=no --loop-playlist=inf --fullscreen --terminal=no --msg-level=all=warn --log-file={mpvLogPath} --playlist={startupPlaylistPath} --input-ipc-server={pipeName}";  // default with named pipe
        private static int checkIntervalMs = 3000; // Check every 3 seconds

        static async Task Main(string[] args)
        {
            // Optional: parse args for executable path and arguments
            if (args.Length > 0)
                mpvExecutable = args[0];
            if (args.Length > 1)
                mpvArguments = args[1];
            if (args.Length > 2)
                pipeName = args[2];

            Console.WriteLine("MPV Watchdog Started.");
            Console.WriteLine($"Executable: {mpvExecutable}");
            Console.WriteLine($"Arguments: {mpvArguments}");
            Console.WriteLine($"Checking every {checkIntervalMs} ms.");
            Console.WriteLine($"Named pipe to check: {pipeName}\n");

            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Stopping watchdog...");
                cts.Cancel();
                e.Cancel = true;
            };

            await MonitorMpvLoopAsync(cts.Token);
        }

        private static async Task MonitorMpvLoopAsync(CancellationToken token)
        {
            Process mpvProcess = null;

            while (!token.IsCancellationRequested)
            {
                bool mpvRunning = IsMpvRunning(out mpvProcess);
                bool pipeExists = false;

                if (mpvRunning)
                {
                    pipeExists = await NamedPipeExistsAsync(pipeName, 10).ConfigureAwait(false);
                }

                if (!mpvRunning)
                {
                    Console.WriteLine($"{DateTime.Now}: MPV not running. Starting MPV...");
                    mpvProcess = StartMpv();
                }
                else if (!pipeExists)
                {
                    Console.WriteLine($"{DateTime.Now}: MPV running but named pipe does not exist. Restarting MPV...");
                    try
                    {
                        if (!mpvProcess.HasExited)
                        {
                            mpvProcess.Kill();
                            mpvProcess.WaitForExit(3000);
                        }
                        mpvProcess.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to kill MPV process: {ex.Message}");
                    }
                    mpvProcess = StartMpv();
                }
                else
                {
                    // MPV running and named pipe exists
                    //Console.WriteLine($"{DateTime.Now}: MPV running and pipe exists.");
                }

                try
                {
                    await Task.Delay(checkIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private static Process StartMpv()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = mpvExecutable,
                    Arguments = mpvArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process proc = Process.Start(startInfo);
                Console.WriteLine("MPV started with PID: " + proc.Id);
                return proc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start MPV: {ex.Message}");
                return null;
            }
        }

        private static bool IsMpvRunning(out Process mpvProcess)
        {
            mpvProcess = null;
            try
            {
                Process[] processes = Process.GetProcessesByName("mpv");
                foreach (var proc in processes)
                {
                    if (!proc.HasExited)
                    {
                        mpvProcess = proc;
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore exceptions and treat as not running
            }
            return false;
        }

        private static Task<bool> NamedPipeExistsAsync(string fullPipeName, int timeoutMs)
        {
            // fullPipeName e.g. "\\.\pipe\mpv_control_pipe"
            // We connect using NamedPipeClientStream with tiny timeout to check existence

            return Task.Run(() =>
            {
                try
                {
                    // The pipe name needed to create NamedPipeClientStream excludes the prefix \\.\pipe\
                    // so extract only the final pipe name part:
                    string pipeShortName = ExtractPipeName(fullPipeName);
                    using (var pipeClient = new NamedPipeClientStream(".", pipeShortName, PipeDirection.Out))
                    {
                        pipeClient.Connect(timeoutMs);
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        private static string ExtractPipeName(string fullPipeName)
        {
            // Assume format \\.\pipe\<pipename>
            var prefix = @"\\.\pipe\";
            if (fullPipeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPipeName.Substring(prefix.Length);
            }
            return fullPipeName;
        }
    }
}