using System;
using TextLength.Models;

namespace TextLength.Services
{
    public interface ITextSelectionService
    {
        event EventHandler<TextSelectionInfo> TextSelectionChanged;
        
        void Initialize();
        void Shutdown();
        TextSelectionInfo GetCurrentSelection();
        void ToggleEnabled(bool isEnabled);
        void UpdateSettings(AppSettings newSettings);
    }
} 