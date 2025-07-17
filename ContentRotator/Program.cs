using ContentRotator;

// Переменные, которые нужно вынести в конфигурационные файлы
var remoteContentFolderPath = @"c:\data\VideoKiosk\remote";
var localContentFolderPath = @"c:\data\VideoKiosk\local";
var pipeName = @"mpv_control_pipe";

// Optional: parse args for executable path and arguments
if (args.Length > 0)
    remoteContentFolderPath = args[0];
if (args.Length > 1)
    localContentFolderPath = args[1];
if (args.Length > 2)
    pipeName = args[2];

Console.WriteLine("Content Rotator Started.");
Console.WriteLine($"Remote content folder: {remoteContentFolderPath}");
Console.WriteLine($"Local cache folder: {localContentFolderPath}");
Console.WriteLine($"Named pipe to send commands: {pipeName}\n");
//
var controlPipe = new NamedPipeClient(pipeName);
var сontentManager = new ContentManager(localContentFolderPath, remoteContentFolderPath);

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
