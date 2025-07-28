using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Configuration;
using CommonLib;

namespace MpvWatchdogApp
{
    class MpvWatchdog
    {
        private static AppSettings Settings = new AppSettings();
        static async Task Main(string[] args)
        {

            var config = new ConfigurationBuilder()
                            .SetBasePath(AppContext.BaseDirectory) // base directory to find appsettings.json
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .Build();
            // Bind config to strongly-typed class
            config.Bind(Settings);

            var loggerMpvWatchdog = new FileLogger(Settings.WatchdogLogPath);

            loggerMpvWatchdog.Info("MPV Watchdog Started.");
            loggerMpvWatchdog.Info($"Executable: {Settings.MpvExecutable}");
            loggerMpvWatchdog.Info($"Startup Playlist Path: {Settings.StartupPlaylistPath}");
            loggerMpvWatchdog.Info($"Checking every {Settings.CheckIntervalMs} ms.");
            loggerMpvWatchdog.Info($"Named pipe to check: {Settings.PipeName}");
            loggerMpvWatchdog.Info($"MPV logfile: {Settings.MpvLogPath}");

            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                loggerMpvWatchdog.Info($"Stopping watchdog");
                Console.WriteLine("Stopping watchdog...");
                cts.Cancel();
                e.Cancel = true;
            };

            await MonitorMpvLoopAsync(cts.Token, loggerMpvWatchdog);
        }

        private static async Task MonitorMpvLoopAsync(CancellationToken token, FileLogger loggerMpvWatchdog)
        {
            Process mpvProcess = null;

            while (!token.IsCancellationRequested)
            {
                bool mpvRunning = IsMpvRunning(out mpvProcess);
                bool pipeExists = false;

                if (mpvRunning)
                {
                    pipeExists = await NamedPipeExistsAsync(Settings.PipeName, 10).ConfigureAwait(false);
                }

                if (!mpvRunning)
                {
                    var msgText = $"MPV not running.";
                    loggerMpvWatchdog.Info(msgText);
                    Console.WriteLine($"{DateTime.Now}: {msgText}");
                    mpvProcess = StartMpv();
                }
                else if (!pipeExists)
                {
                    var msgText = $"MPV running but named pipe {Settings.PipeName} does not exist. Restarting MPV...";
                    loggerMpvWatchdog.Info(msgText);
                    Console.WriteLine($"{DateTime.Now}: {msgText}");
                    try
                    {
                        if (!mpvProcess.HasExited)
                        {
                            msgText = $"Killing MPV process with PID {mpvProcess.Id}...";
                            loggerMpvWatchdog.Info(msgText);
                            mpvProcess.Kill();
                            mpvProcess.WaitForExit(3000);
                        }
                        mpvProcess.Dispose();
                    }
                    catch (Exception ex)
                    {
                        msgText = $"Failed to kill MPV process: {ex.Message}";
                        loggerMpvWatchdog.Error(msgText);
                        Console.WriteLine($"{DateTime.Now}: {msgText}");
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
                    await Task.Delay(Settings.CheckIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private static Process StartMpv()
        {
            string mpvArguments = @$"--audio=no --border=no --title-bar=no --loop-playlist=inf --fullscreen --terminal=no --msg-level=all=warn --log-file={Settings.MpvLogPath} --playlist={Settings.StartupPlaylistPath} --input-ipc-server={Settings.PipeName}";
            Console.WriteLine($"{ mpvArguments}");
            // Проверим, существует ли стартовый плейлист
            if (!File.Exists(Settings.StartupPlaylistPath))
                File.Create(Settings.StartupPlaylistPath);
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Settings.MpvExecutable,
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

        private static Task<bool> NamedPipeExistsAsync(string pipeName, int timeoutMs)
        {
            // fullsettings.PipeName e.g. "\\.\pipe\mpv_control_pipe"
            // We connect using NamedPipeClientStream with tiny timeout to check existence

            return Task.Run(() =>
            {
                try
                {
                    // The pipe name needed to create NamedPipeClientStream excludes the prefix \\.\pipe\
                    // so extract only the final pipe name part:
                    string pipeShortName = ExtractPipeName(pipeName);
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
            // Assume format \\.\pipe\<settings.PipeName>
            var prefix = @"\\.\pipe\";
            if (fullPipeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return fullPipeName.Substring(prefix.Length);
            }
            return fullPipeName;
        }
    }
}