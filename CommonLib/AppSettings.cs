using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonLib
{
    public class AppSettings
    {
        public string MpvExecutable { get; set; }
        public string PipeName { get; set; }
        public string RemoteContentFolderPath { get; set; }
        public string LocalContentFolderPath { get; set; }
        public string StartupPlaylistPath { get; set; }
        public string MpvLogPath { get; set; }
        public string WatchdogLogPath { get; set; }
        public string ContentRotatorLogPath { get; set; }
        public int CheckIntervalMs { get; set; }
        public List<string> ContentSubfolders { get; set; }
        public string IncompleteTaskMarker { get; set; }
        public string DefaultPlayListName { get; set; }
        public List<string> AllowedExtensions { get; set; }
    }
}
