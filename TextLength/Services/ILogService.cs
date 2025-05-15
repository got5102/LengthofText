using System;

namespace TextLength.Services
{
    public interface ILogService
    {
        void LogInfo(string message);
        void LogDebug(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
        void LogTextSelection(string selectedText, int characterCount, int wordCount);
        string GetLogDirectoryPath();
    }
} 