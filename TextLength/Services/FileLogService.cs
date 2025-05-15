using System;
using System.IO;
using TextLength.Models;

namespace TextLength.Services
{
    public class FileLogService : ILogService
    {
        private readonly AppSettings _settings;
        private readonly string _basePath;

        public FileLogService(AppSettings settings)
        {
            _settings = settings;
            _basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TextLength", 
                settings.LogPath);
            
            // ログディレクトリを作成
            EnsureLogDirectoryExists();
        }

        private void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_basePath))
                {
                    Directory.CreateDirectory(_basePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ログディレクトリの作成に失敗しました: {ex.Message}");
            }
        }

        public void LogInfo(string message)
        {
            if (!_settings.LoggingEnabled) return;
            
            WriteToLog("INFO", message);
        }

        public void LogDebug(string message)
        {
            if (!_settings.LoggingEnabled) return;
            WriteToLog("DEBUG", message);
        }

        public void LogWarning(string message)
        {
            if (!_settings.LoggingEnabled) return;
            WriteToLog("WARNING", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            string logMessage = message;
            if (exception != null)
            {
                logMessage += $" - 例外: {exception.Message}";
                logMessage += $" - スタックトレース: {exception.StackTrace}";
            }
            
            WriteToLog("ERROR", logMessage);
        }

        public void LogTextSelection(string selectedText, int characterCount, int wordCount)
        {
            if (!_settings.LoggingEnabled) return;
            
            string message = $"選択文字: {characterCount}字 / {wordCount}語 - テキスト: {(selectedText.Length > 100 ? selectedText.Substring(0, 100) + "..." : selectedText)}";
            WriteToLog("SELECTION", message);
        }

        private void WriteToLog(string level, string message)
        {
            try
            {
                string logFileName = $"TextLength_{DateTime.Now:yyyy-MM-dd}.log";
                string logFilePath = Path.Combine(_basePath, logFileName);
                
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                
                using (var writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ログの書き込みに失敗しました: {ex.Message}");
            }
        }

        public string GetLogDirectoryPath()
        {
            return _basePath;
        }
    }
} 