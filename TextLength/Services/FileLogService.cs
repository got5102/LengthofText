using System;
using System.IO;
using TextLength.Models;

namespace TextLength.Services
{
    // ファイルにログを書き込むサービス
    public class FileLogService : ILogService
    {
        private readonly AppSettings _settings;
        private readonly string _basePath;

        // 設定を受け取って初期化。ログディレクトリも作成
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

        // ログディレクトリがなければ作成
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

        // 情報ログを出力
        public void LogInfo(string message)
        {
            if (!_settings.LoggingEnabled) return;
            
            WriteToLog("INFO", message);
        }

        // デバッグログを出力
        public void LogDebug(string message)
        {
            if (!_settings.LoggingEnabled) return;
            WriteToLog("DEBUG", message);
        }

        // 警告ログを出力
        public void LogWarning(string message)
        {
            if (!_settings.LoggingEnabled) return;
            WriteToLog("WARNING", message);
        }

        // エラーログを出力
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

        // テキスト選択の内容をログに記録
        public void LogTextSelection(string selectedText, int characterCount, int wordCount)
        {
            if (!_settings.LoggingEnabled) return;
            
            string message = $"選択文字: {characterCount}字 / {wordCount}語 - テキスト: {(selectedText.Length > 100 ? selectedText.Substring(0, 100) + "..." : selectedText)}";
            WriteToLog("SELECTION", message);
        }

        // 実際にファイルへ書き込む処理
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

        // ログディレクトリのパスを返す
        public string GetLogDirectoryPath()
        {
            return _basePath;
        }
    }
} 