using System.Windows.Forms;
using System;

namespace TextLength.Models
{
    public class AppSettings : ICloneable
    {
        public bool AutoStartEnabled { get; set; } = true;
        public bool IgnoreSpaces { get; set; } = false;
        public bool IgnoreLineBreaks { get; set; } = false;
        public bool ShowWordCount { get; set; } = true;
        public bool ShowCountsInContextMenu { get; set; } = true;
        public double OverlayDuration { get; set; } = 1.5;
        public string OverlayPosition { get; set; } = "Cursor";
        public int FontSize { get; set; } = 14;
        public string FontColor { get; set; } = "#FFFFFF";
        public string BackgroundColor { get; set; } = "#7F000000";
        public string Language { get; set; } = "ja-JP";
        public bool LoggingEnabled { get; set; } = true;
        public string LogPath { get; set; } = "Logs";
        public Keys ShortcutKey { get; set; } = Keys.C;
        public Keys ShortcutModifiers { get; set; } = Keys.Control;

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
} 