using ContentRotator;
using Microsoft.Extensions.Configuration;
using MpvWatchdogApp;

var config = new ConfigurationBuilder()
                           .SetBasePath(AppContext.BaseDirectory) // base directory to find appsettings.json
                           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                           .Build();
// Bind config to strongly-typed class
var settings = new AppSettings();
config.Bind(settings);

Console.WriteLine("Content Rotator Started.");
Console.WriteLine($"Remote content folder: {settings.RemoteContentFolderPath}");
Console.WriteLine($"Local cache folder: {settings.LocalContentFolderPath}");
Console.WriteLine($"Named pipe to send commands: {settings.PipeName}");
//
var controlPipe = new NamedPipeClient(settings.PipeName);
var сontentManager = new ContentManager(settings);

// обновим локальный кэш
var newPlaylistPath = сontentManager.MakeLocalCache();

//запустим новые медиафайлы на проигрывание
var commandText = "playlist-clear";
controlPipe.SendCommand(commandText);
commandText = @$"loadlist {newPlaylistPath} insert-next";
controlPipe.SendCommand(commandText);

//очистим остальные папки
string currentPath = Path.GetDirectoryName(newPlaylistPath);
Console.WriteLine(newPlaylistPath);

foreach (var path in сontentManager.LocalMediaContentFolders.Where(p => !p.Equals(currentPath)))
{
    if (Directory.Exists(path))
    {
        var incompleteCopyTaskMarker = сontentManager.IncompleteCopyTaskMarker(path);
        // На всякий случай
        using (FileStream fs = File.Create(incompleteCopyTaskMarker))
        {
            // Пустой файл
        }
        string[] files = Directory.GetFiles(path);
        bool taskCopleted = true;
        foreach (string file in files.Where(f => !f.Equals(incompleteCopyTaskMarker)))
        {
            try
            {
                File.Delete(file);
                Console.WriteLine($"Deleted file: {file}");
            }
            catch (IOException)
            {
                // File is likely busy or locked; skip it silently or log if needed
                Console.WriteLine($"Skipped busy or locked file: {file}");
                taskCopleted = false;
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to delete; skip or log
                Console.WriteLine($"Skipped unauthorized file: {file}");
                taskCopleted = false;
            }
            catch (Exception ex)
            {
                // Other exceptions: log and continue
                Console.WriteLine($"Failed to delete file {file}: {ex.Message}");
                taskCopleted = false;
            }
        }
        if(taskCopleted)
        {
            File.Delete(incompleteCopyTaskMarker);
        }
    }
}
