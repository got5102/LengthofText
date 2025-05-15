using System.Windows.Forms;
using System;

namespace TextLength.Models
{
    public class AppSettings : ICloneable
    {
        // Windows起動時に自動起動するか
        public bool AutoStartEnabled { get; set; } = true;
        // 選択テキストの空白を無視するか
        public bool IgnoreSpaces { get; set; } = false;
        // 選択テキストの改行を無視するか
        public bool IgnoreLineBreaks { get; set; } = false;
        // 単語数も表示するか
        public bool ShowWordCount { get; set; } = true;
        // コンテキストメニューにもカウントを表示するか
        public bool ShowCountsInContextMenu { get; set; } = true;
        // オーバーレイの表示時間（秒）
        public double OverlayDuration { get; set; } = 1.5;
        // オーバーレイの表示位置
        public string OverlayPosition { get; set; } = "Cursor";
        // フォントサイズ
        public int FontSize { get; set; } = 14;
        // 文字色
        public string FontColor { get; set; } = "#FFFFFF";
        // 背景色
        public string BackgroundColor { get; set; } = "#7F000000";
        // 使用言語
        public string Language { get; set; } = "ja-JP";
        // ログ記録を有効にするか
        public bool LoggingEnabled { get; set; } = true;
        // ログの保存先ディレクトリ
        public string LogPath { get; set; } = "Logs";
        // ショートカットキー
        public Keys ShortcutKey { get; set; } = Keys.C;
        // ショートカットキーの修飾キー
        public Keys ShortcutModifiers { get; set; } = Keys.Control;

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
} 