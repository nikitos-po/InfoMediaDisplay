using ContentRotator;
using Microsoft.Extensions.Configuration;
using CommonLib;

var config = new ConfigurationBuilder()
                           .SetBasePath(AppContext.BaseDirectory) // base directory to find appsettings.json
                           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                           .Build();
// Bind config to strongly-typed class
var settings = new AppSettings();
config.Bind(settings);

var loggerContentRotator = new FileLogger(settings.ContentRotatorLogPath);

loggerContentRotator.Info("Content Rotator Started.");
loggerContentRotator.Info($"Launched on behalf of: {Environment.UserDomainName}\\{Environment.UserName}");
loggerContentRotator.Info($"Remote content folder: {settings.RemoteContentFolderPath}");
loggerContentRotator.Info($"Local cache folder: {settings.LocalContentFolderPath}");
loggerContentRotator.Info($"Named pipe to send commands: {settings.PipeName}");
loggerContentRotator.Info($"Checking every {settings.CheckIntervalMs} ms.");
//
var controlPipe = new NamedPipeClient(settings.PipeName, loggerContentRotator);
var сontentManager = new ContentManager(settings, loggerContentRotator);

// обновим локальный кэш
var newPlaylistPath = сontentManager.MakeLocalCache();

//запустим новые медиафайлы на проигрывание
var commandText = "playlist-clear";
controlPipe.SendCommand(commandText);
commandText = @$"loadlist {newPlaylistPath} insert-next";
controlPipe.SendCommand(commandText);

//очистим остальные папки
string currentPath = Path.GetDirectoryName(newPlaylistPath);
if (string.IsNullOrEmpty(currentPath))
{
    var errMsgText = "Failed to determine current path from the new playlist path.";
    loggerContentRotator.Error(errMsgText);
    Console.WriteLine(errMsgText);
    return;
}
Console.WriteLine(newPlaylistPath);
loggerContentRotator.Info($"Current path: {currentPath}");

foreach (var path in сontentManager.LocalMediaContentFolders.Where(p => !p.Equals(currentPath)))
{
    if (Directory.Exists(path))
    {
        var incompleteCopyTaskMarker = сontentManager.IncompleteCopyTaskMarker(path);
        // На всякий случай
        using (FileStream fs = File.Create(incompleteCopyTaskMarker))
        {
            // Пустой файл
            loggerContentRotator.Debug($"Created incomplete copy task marker: {incompleteCopyTaskMarker}");
        }
        string[] files = Directory.GetFiles(path);
        bool taskCopleted = true;
        foreach (string file in files.Where(f => !f.Equals(incompleteCopyTaskMarker)))
        {
            try
            {
                File.Delete(file);
                var msgText = $"Deleted file: {file}";
                loggerContentRotator.Debug(msgText);
                Console.WriteLine(msgText);
            }
            catch (IOException)
            {
                // File is likely busy or locked; skip it silently or log if needed
                var msgText = $"Skipped busy or locked file: {file}";
                loggerContentRotator.Warning(msgText);
                Console.WriteLine(msgText);
                taskCopleted = false;
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to delete; skip or log
                var msgText = $"Skipped unauthorized file: {file}";
                loggerContentRotator.Warning(msgText);
                Console.WriteLine(msgText);
                taskCopleted = false;
            }
            catch (Exception ex)
            {
                // Other exceptions: log and continue
                var msgText = $"Failed to delete file {file}: {ex.Message}";
                loggerContentRotator.Error(msgText);
                Console.WriteLine(msgText);
                taskCopleted = false;
            }
        }
        if (taskCopleted)
        {
            File.Delete(incompleteCopyTaskMarker);
            loggerContentRotator.Debug($"Deleted incomplete copy task marker: {incompleteCopyTaskMarker}");
        }
    }
}
