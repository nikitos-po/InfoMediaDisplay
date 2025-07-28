using CommonLib;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Reflection.PortableExecutable;

namespace ContentRotator
{
    /// <summary>
    /// Класс для работы с контентом.
    /// Управляет папками кэша и плейлистами, связанными с ними.
    /// </summary>
    public class ContentManager
    {
        private readonly AppSettings _settings;
        private readonly FileLogger _logger;
        public ContentManager(AppSettings settings, FileLogger logger)
        {
            _settings = settings;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_settings.LocalContentFolderPath))
            {
                var errMsgText = $"Folder path cannot be null or whitespace. {nameof(_settings.LocalContentFolderPath)}";
                _logger.Error(errMsgText);
                throw new ArgumentException(errMsgText);
            }

            if (!Directory.Exists(_settings.LocalContentFolderPath))
            {
                var errMsgText = $"Folder does not exist: {_settings.LocalContentFolderPath}";
                _logger.Error(errMsgText);
                throw new DirectoryNotFoundException(errMsgText);
            }

            if (string.IsNullOrWhiteSpace(_settings.RemoteContentFolderPath))
            {
                var errMsgText = $"Folder path cannot be null or whitespace. {nameof(_settings.RemoteContentFolderPath)}";
                _logger.Error(errMsgText);
                throw new ArgumentException(errMsgText);
            }

            if (!Directory.Exists(_settings.RemoteContentFolderPath))
            {
                var errMsgText = $"Folder does not exist: {_settings.RemoteContentFolderPath}";
                _logger.Error(errMsgText);
                throw new DirectoryNotFoundException(errMsgText);
            }

        }

        public string[] LocalMediaContentFolders
        {
            get
            {
                List<String> result = new List<String>();
                foreach (var subfolder in _settings.ContentSubfolders)
                {
                    string mediafolderPath = Path.Join(_settings.LocalContentFolderPath, subfolder);
                    result.Add(mediafolderPath);
                }
                return result.ToArray();
            }
        }
        public string IncompleteCopyTaskMarker(string folderPath)
        {
            return Path.Join(folderPath, _settings.IncompleteTaskMarker);
        }
        /// <summary>
        /// Определяем первую пустую папку.
        /// В неё будет загружать кэш медиафайлов.
        /// </summary>
        /// <returns></returns>
        public string DetermineLocalMediaFolderPath()
        {
            string mediafolderPath = _settings.LocalContentFolderPath;
            // Ищем пустую папку
            foreach (var subfolder in LocalMediaContentFolders)
            {
                if (!Directory.Exists(subfolder))
                {
                    Directory.CreateDirectory(subfolder);
                    _logger.Debug($"Created new media folder: {subfolder}");
                    return subfolder;
                }
                if (!HasAnyFile(subfolder))
                {
                    return subfolder;
                }
            }
            // Пустую папку не нашли, проверяем есть ли папки с незавершённой задачей копирования
            foreach (var subfolder in LocalMediaContentFolders)
            {
                
                if (File.Exists(IncompleteCopyTaskMarker(subfolder)))
                {
                    return subfolder;
                }
            }
            var errMsgText = $"No suitable folders found in {_settings.LocalContentFolderPath}.";
            _logger.Error(errMsgText);
            throw new ArgumentException(errMsgText);
        }

        /// <summary>
        /// Checks if the specified folder contains any files.
        /// </summary>
        /// <param name="folderPath">The path of the folder to check.</param>
        /// <returns>True if any file exists in the folder; otherwise, false.</returns>
        public static bool HasAnyFile(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                var errMsgText = $"Folder path cannot be null or whitespace. {nameof(folderPath)}";
                throw new ArgumentException(errMsgText);
            }

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder does not exist: {folderPath}");

            // Check if any files exist in the folder (not searching subdirectories)
            string[] files = Directory.GetFiles(folderPath);
            return files.Length > 0;
        }
        /// <summary>
        /// Копируем из источника медиафайлы с разрешёнными расширениями
        /// </summary>
        /// <returns>
        /// Путь к новому плейлисту
        /// </returns>
        public string MakeLocalCache()
        {
            List<String> mediaFiles = new List<String>();
            string defaultPlaylistPath = _settings.StartupPlaylistPath;
            string localMediaFolder = DetermineLocalMediaFolderPath();
            string newPlaylistPath = Path.Join(localMediaFolder, "playlist.txt");
            string incompleteCopyTaskMarker = IncompleteCopyTaskMarker(localMediaFolder);
            // Чистим папку, на случай если были незавершённые задачи
            string[] files = Directory.GetFiles(localMediaFolder);
            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    var errMsgText = $"Failed to delete file {file}: {ex.Message}";
                    _logger.Error(errMsgText);
                    Console.WriteLine(errMsgText);
                }
            }
            // Создаём маркер
            using (FileStream fs = File.Create(incompleteCopyTaskMarker))
            {
                // Пустой файл
            }
            //Копируем файлы
            string[] filesToCopy = Directory.GetFiles(_settings.RemoteContentFolderPath);
            foreach (string filePath in filesToCopy)
            {
                string fileName = Path.GetFileName(filePath);
                string fileExtension = Path.GetExtension(filePath);
                string destFilePath = Path.Join(localMediaFolder, fileName);
                // Только файлы с допустимыми расширениями берём в работу
                if (_settings.AllowedExtensions.Contains(fileExtension))
                {
                    File.Copy(filePath, destFilePath, true);
                    // Сохраняем в список, чтобы вернуть
                    mediaFiles.Add(destFilePath);
                }
            }
            // Создаём плейлисты
            File.WriteAllLines(newPlaylistPath, mediaFiles);
            File.WriteAllLines(defaultPlaylistPath, mediaFiles);
            // Удаляем маркер
            File.Delete(incompleteCopyTaskMarker);
            //
            return newPlaylistPath;
        }
    }
}
