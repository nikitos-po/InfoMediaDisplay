using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ContentRotator
{
    /// <summary>
    /// Класс для работы с контентом.
    /// Управляет папками кэша и плейлистами, связанными с ними.
    /// </summary>
    public class ContentManager
    {
        public static readonly string[] ContentSubfolders = { @"\a", @"\b" };
        public static readonly string IncompleteTaskMarker = @"\incomplete";
        public static readonly string DefaultPlayListName = @"\default.txt";
        public static readonly string[] AlowedExtensions = { ".mp4", ".jpg", ".png" };

        private readonly string _localContentFolderPath;
        private readonly string _remoteContentFolderPath;
        public ContentManager(string localContentFolderPath, string remoteContentFolderPath)
        {
            if (string.IsNullOrWhiteSpace(localContentFolderPath))
                throw new ArgumentException("Folder path cannot be null or whitespace.", nameof(localContentFolderPath));

            if (!Directory.Exists(localContentFolderPath))
                throw new DirectoryNotFoundException($"Folder does not exist: {localContentFolderPath}");

            if (string.IsNullOrWhiteSpace(remoteContentFolderPath))
                throw new ArgumentException("Folder path cannot be null or whitespace.", nameof(remoteContentFolderPath));

            if (!Directory.Exists(remoteContentFolderPath))
                throw new DirectoryNotFoundException($"Folder does not exist: {remoteContentFolderPath}");

            _localContentFolderPath = localContentFolderPath;
            _remoteContentFolderPath = remoteContentFolderPath;
        }

        public string[] LocalMediaContentFolders
        {
            get
            {
                List<String> result = new List<String>();
                foreach (var subfolder in ContentSubfolders)
                {
                    string mediafolderPath = Path.Join(_localContentFolderPath, subfolder);
                    result.Add(mediafolderPath);
                }
                return result.ToArray();
            }
        }
        public string IncompleteCopyTaskMarker(string folderPath)
        {
            return Path.Join(folderPath, IncompleteTaskMarker);
        }
        /// <summary>
        /// Определяем первую пустую папку.
        /// В неё будет загружать кэш медиафайлов.
        /// </summary>
        /// <returns></returns>
        public string DetermineLocalMediaFolderPath()
        {
            string mediafolderPath = _localContentFolderPath;
            // Ищем пустую папку
            foreach (var subfolder in LocalMediaContentFolders)
            {
                if (!Directory.Exists(subfolder))
                {
                    Directory.CreateDirectory(subfolder);
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
            throw new ArgumentException("No sutable folders.", nameof(_localContentFolderPath));
        }

        /// <summary>
        /// Checks if the specified folder contains any files.
        /// </summary>
        /// <param name="folderPath">The path of the folder to check.</param>
        /// <returns>True if any file exists in the folder; otherwise, false.</returns>
        public static bool HasAnyFile(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path cannot be null or whitespace.", nameof(folderPath));

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
            string defaultPlaylistPath = Path.Join(_localContentFolderPath, DefaultPlayListName);
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
                    Console.WriteLine($"Failed to delete file {file}: {ex.Message}");
                }
            }
            // Создаём маркер
            using (FileStream fs = File.Create(incompleteCopyTaskMarker))
            {
                // Пустой файл
            }
            //Копируем файлы
            string[] filesToCopy = Directory.GetFiles(_remoteContentFolderPath);
            foreach (string filePath in filesToCopy)
            {
                string fileName = Path.GetFileName(filePath);
                string fileExtension = Path.GetExtension(filePath);
                string destFilePath = Path.Join(localMediaFolder, fileName);
                // Только файлы с допустимыми расширениями берём в работу
                if (AlowedExtensions.Contains(fileExtension))
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
