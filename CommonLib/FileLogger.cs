using System;
using System.IO;
using System.Text;
namespace CommonLib
{
    public class FileLogger
    {
        private readonly string logFilePath;
        private readonly object lockObj = new object();

        public FileLogger(string logFilePath)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
                throw new ArgumentException("logFilePath cannot be null or empty");

            this.logFilePath = logFilePath;

            // Create the log file if it doesn't exist
            if (!File.Exists(logFilePath))
            {
                using (var stream = File.Create(logFilePath)) { }
            }
        }

        private void Log(string level, string message)
        {
            var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

            lock (lockObj)
            {
                File.AppendAllText(logFilePath, logRecord, Encoding.UTF8);
            }
        }

        public void Debug(string message) => Log("INFO", message);
        public void Info(string message) => Log("INFO", message);

        public void Warning(string message) => Log("WARN", message);

        public void Error(string message) => Log("ERROR", message);

        public void Error(Exception ex) => Log("ERROR", $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
    }
}