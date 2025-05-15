using System.Windows;
using System.Linq;
using System.Diagnostics;

namespace TextLength.Models
{
    /// <summary>
    /// 選択されたテキストの情報を保持します。
    /// </summary>
    public class TextSelectionInfo
    {
        /// <summary>
        /// 選択されたテキスト。
        /// </summary>
        private string _selectedText = string.Empty;
        
        public string SelectedText 
        { 
            get => _selectedText;
            set => _selectedText = value ?? string.Empty; // null保護
        }
        
        /// <summary>
        /// 文字数。
        /// </summary>
        public int CharacterCount { get; set; }
        
        /// <summary>
        /// 単語数。
        /// </summary>
        public int WordCount { get; set; }
        
        /// <summary>
        /// テキスト選択の終了位置（オーバーレイ表示の基準点など）。
        /// </summary>
        public Point SelectionEndPoint { get; set; }
        
        /// <summary>
        /// この情報がアクティブな選択（例：ユーザーが現在選択している範囲）からのものか。
        /// </summary>
        private bool _isActive;
        public bool IsActive 
        { 
            get => _isActive; 
            // 内部的に設定可能に
            internal set => _isActive = value;
        }

        /// <summary>
        /// この情報がキーボードショートカットによってトリガーされたかどうかを示します。
        /// </summary>
        public bool TriggeredByShortcut { get; }

        /// <summary>
        /// デフォルトコンストラクタ（主にダミーデータや初期化用）。
        /// </summary>
        public TextSelectionInfo()
        {
            // デフォルト値を明示的に設定
            _selectedText = string.Empty;
            CharacterCount = 0;
            WordCount = 0;
            SelectionEndPoint = new Point(0, 0);
            _isActive = false;
            TriggeredByShortcut = false;
        }

        /// <summary>
        /// 完全な情報を持つ TextSelectionInfo を作成します。
        /// </summary>
        /// <param name="selectedText">選択されたテキスト。</param>
        /// <param name="selectionEndPoint">テキスト選択の終了位置。</param>
        /// <param name="characterCount">文字数。</param>
        /// <param name="wordCount">単語数。</param>
        /// <param name="isActive">アクティブな選択からの情報か（デフォルトはtrue）。</param>
        /// <param name="triggeredByShortcut">キーボードショートカットによってトリガーされたか（デフォルトはfalse）。</param>
        public TextSelectionInfo(
            string selectedText, 
            Point selectionEndPoint, 
            int characterCount, 
            int wordCount,
            bool isActive = true,
            bool triggeredByShortcut = false)
        {
            SelectedText = selectedText ?? string.Empty; // null保護
            SelectionEndPoint = selectionEndPoint;
            CharacterCount = characterCount;
            WordCount = wordCount;
            _isActive = isActive;
            TriggeredByShortcut = triggeredByShortcut;
            
            Debug.WriteLine($"TextSelectionInfo作成: 文字数={CharacterCount}, 単語数={WordCount}, Active={_isActive}");
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            return text.Split(new[] { ' ', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }

        // ToString()をオーバーライドして調査しやすくする
        public override string ToString()
        {
            return $"文字数: {CharacterCount}, 単語数: {WordCount}, 選択テキスト長: {SelectedText?.Length ?? 0}, Active: {IsActive}";
        }
    }
} 