using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Media;
using TextLength.Models;
using TextLength.Services;
using System.Windows; // Windowクラスのために追加
using System.Windows.Forms; // Keys型のために追加
using System.Collections.Generic; // List型のために追加
using System.Diagnostics; // Debug用
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Threading;

namespace TextLength.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly IStartupService _startupService;
        private readonly ILogService _logService;
        private readonly IOverlayService _overlayService;
        private readonly SettingsService _settingsService;
        private readonly ITextSelectionService _textSelectionService;

        private AppSettings _originalSettings; // 初期読み込み時の設定を保持
        private AppSettings _currentSettings;  // UIにバインドする現在の設定
        
        public bool AutoStartEnabled
        {
            get => _currentSettings.AutoStartEnabled;
            set
            {
                if (_currentSettings.AutoStartEnabled != value)
                {
                    _currentSettings.AutoStartEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IgnoreSpaces
        {
            get => _currentSettings.IgnoreSpaces;
            set
            {
                if (_currentSettings.IgnoreSpaces != value)
                {
                    _currentSettings.IgnoreSpaces = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IgnoreLineBreaks
        {
            get => _currentSettings.IgnoreLineBreaks;
            set
            {
                if (_currentSettings.IgnoreLineBreaks != value)
                {
                    _currentSettings.IgnoreLineBreaks = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public bool ShowWordCount
        {
            get => _currentSettings.ShowWordCount;
            set
            {
                if (_currentSettings.ShowWordCount != value)
                {
                    _currentSettings.ShowWordCount = value;
                    OnPropertyChanged();
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public double DisplayDuration
        {
            get => _currentSettings.OverlayDuration;
            set
            {
                if (_currentSettings.OverlayDuration != value)
                {
                    _currentSettings.OverlayDuration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedDisplayDuration));
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public string FormattedDisplayDuration
        {
            get => $"{_currentSettings.OverlayDuration:F1}秒";
        }
        
        private string _selectedOverlayPosition;
        public string SelectedOverlayPosition
        {
            get => _selectedOverlayPosition;
            set
            {
                if (_selectedOverlayPosition != value)
                {
                    _selectedOverlayPosition = value;
                    _currentSettings.OverlayPosition = value;
                    OnPropertyChanged();
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public ObservableCollection<string> OverlayPositionOptions { get; } = new ObservableCollection<string>
        {
            "Cursor",
            "RightBottom",
            "LeftBottom",
            "RightTop",
            "LeftTop"
        };
        
        public int FontSize
        {
            get => _currentSettings.FontSize;
            set
            {
                if (_currentSettings.FontSize != value)
                {
                    _currentSettings.FontSize = value;
                    OnPropertyChanged();
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public string FontColor
        {
            get => _currentSettings.FontColor;
            set
            {
                if (_currentSettings.FontColor != value)
                {
                    _currentSettings.FontColor = value;
                    OnPropertyChanged();
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public string BackgroundColor
        {
            get => _currentSettings.BackgroundColor;
            set
            {
                if (_currentSettings.BackgroundColor != value)
                {
                    _currentSettings.BackgroundColor = value;
                    OnPropertyChanged();
                    _overlayService.UpdateSettings();
                }
            }
        }
        
        public string Language
        {
            get => _currentSettings.Language;
            set
            {
                if (_currentSettings.Language != value)
                {
                    _currentSettings.Language = value;
                    OnPropertyChanged();
                    OnLanguageChanged(value);
                }
            }
        }
        
        // 言語変更通知イベント
        public event EventHandler<string>? LanguageChanged;
        
        protected virtual void OnLanguageChanged(string language)
        {
            LanguageChanged?.Invoke(this, language);
        }
        
        public ObservableCollection<string> LanguageOptions { get; } = new ObservableCollection<string>
        {
            "ja-JP",
            "en-US"
        };
        
        public bool LoggingEnabled
        {
            get => _currentSettings.LoggingEnabled;
            set
            {
                if (_currentSettings.LoggingEnabled != value)
                {
                    _currentSettings.LoggingEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public Keys ShortcutKey
        {
            get => _currentSettings.ShortcutKey;
            set
            {
                if (_currentSettings.ShortcutKey != value)
                {
                    _currentSettings.ShortcutKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public Keys ShortcutModifiers
        {
            get => _currentSettings.ShortcutModifiers;
            set
            {
                if (_currentSettings.ShortcutModifiers != value)
                {
                    _currentSettings.ShortcutModifiers = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<KeyValuePair<Keys, string>> AvailableKeys => GetAvailableKeys();
        public List<KeyValuePair<Keys, string>> AvailableModifiers => GetAvailableModifiers();

        private List<KeyValuePair<Keys, string>> GetAvailableKeys()
        {
            return new List<KeyValuePair<Keys, string>>
            {
                new KeyValuePair<Keys, string>(Keys.A, "A"),
                new KeyValuePair<Keys, string>(Keys.B, "B"),
                new KeyValuePair<Keys, string>(Keys.C, "C"),
                new KeyValuePair<Keys, string>(Keys.D, "D"),
                new KeyValuePair<Keys, string>(Keys.E, "E"),
                new KeyValuePair<Keys, string>(Keys.F, "F"),
                new KeyValuePair<Keys, string>(Keys.G, "G"),
                new KeyValuePair<Keys, string>(Keys.H, "H"),
                new KeyValuePair<Keys, string>(Keys.I, "I"),
                new KeyValuePair<Keys, string>(Keys.J, "J"),
                new KeyValuePair<Keys, string>(Keys.K, "K"),
                new KeyValuePair<Keys, string>(Keys.L, "L"),
                new KeyValuePair<Keys, string>(Keys.M, "M"),
                new KeyValuePair<Keys, string>(Keys.N, "N"),
                new KeyValuePair<Keys, string>(Keys.O, "O"),
                new KeyValuePair<Keys, string>(Keys.P, "P"),
                new KeyValuePair<Keys, string>(Keys.Q, "Q"),
                new KeyValuePair<Keys, string>(Keys.R, "R"),
                new KeyValuePair<Keys, string>(Keys.S, "S"),
                new KeyValuePair<Keys, string>(Keys.T, "T"),
                new KeyValuePair<Keys, string>(Keys.U, "U"),
                new KeyValuePair<Keys, string>(Keys.V, "V"),
                new KeyValuePair<Keys, string>(Keys.W, "W"),
                new KeyValuePair<Keys, string>(Keys.X, "X"),
                new KeyValuePair<Keys, string>(Keys.Y, "Y"),
                new KeyValuePair<Keys, string>(Keys.Z, "Z"),
                new KeyValuePair<Keys, string>(Keys.D0, "0"),
                new KeyValuePair<Keys, string>(Keys.D1, "1"),
                new KeyValuePair<Keys, string>(Keys.D2, "2"),
                new KeyValuePair<Keys, string>(Keys.D3, "3"),
                new KeyValuePair<Keys, string>(Keys.D4, "4"),
                new KeyValuePair<Keys, string>(Keys.D5, "5"),
                new KeyValuePair<Keys, string>(Keys.D6, "6"),
                new KeyValuePair<Keys, string>(Keys.D7, "7"),
                new KeyValuePair<Keys, string>(Keys.D8, "8"),
                new KeyValuePair<Keys, string>(Keys.D9, "9"),
                new KeyValuePair<Keys, string>(Keys.F1, "F1"),
                new KeyValuePair<Keys, string>(Keys.F2, "F2"),
                new KeyValuePair<Keys, string>(Keys.F3, "F3"),
                new KeyValuePair<Keys, string>(Keys.F4, "F4"),
                new KeyValuePair<Keys, string>(Keys.F5, "F5"),
                new KeyValuePair<Keys, string>(Keys.F6, "F6"),
                new KeyValuePair<Keys, string>(Keys.F7, "F7"),
                new KeyValuePair<Keys, string>(Keys.F8, "F8"),
                new KeyValuePair<Keys, string>(Keys.F9, "F9"),
                new KeyValuePair<Keys, string>(Keys.F10, "F10"),
                new KeyValuePair<Keys, string>(Keys.F11, "F11"),
                new KeyValuePair<Keys, string>(Keys.F12, "F12")
            };
        }

        private List<KeyValuePair<Keys, string>> GetAvailableModifiers()
        {
            return new List<KeyValuePair<Keys, string>>
            {
                new KeyValuePair<Keys, string>(Keys.Control, "Ctrl"),
                new KeyValuePair<Keys, string>(Keys.Alt, "Alt"),
                new KeyValuePair<Keys, string>(Keys.Shift, "Shift"),
                new KeyValuePair<Keys, string>(Keys.Control | Keys.Alt, "Ctrl+Alt"),
                new KeyValuePair<Keys, string>(Keys.Control | Keys.Shift, "Ctrl+Shift"),
                new KeyValuePair<Keys, string>(Keys.Alt | Keys.Shift, "Alt+Shift"),
                new KeyValuePair<Keys, string>(Keys.Control | Keys.Alt | Keys.Shift, "Ctrl+Alt+Shift")
            };
        }
        
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand OpenLogDirectoryCommand { get; }
        public RelayCommand ApplyCommand { get; }

        public event EventHandler? RequestClose; // ウィンドウを閉じるリクエスト

        // キーキャプチャ関連プロパティ
        private bool _isCapturingKey = false;
        private string _captureButtonText = "キー入力待機...";
        private string _shortcutKeyDisplayText = "";
        private string _shortcutErrorText = "";
        private Visibility _shortcutErrorVisibility = Visibility.Collapsed;
        private System.Windows.Threading.DispatcherTimer? _captureTimeoutTimer;

        public string CaptureButtonText
        {
            get => _isCapturingKey ? "キャンセル" : "キー入力待機...";
            set
            {
                if (_captureButtonText != value)
                {
                    _captureButtonText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ShortcutKeyDisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(_shortcutKeyDisplayText))
                {
                    // 現在の設定からテキストを生成
                    string modifiersText = GetModifierText(_currentSettings.ShortcutModifiers);
                    string keyText = GetKeyText(_currentSettings.ShortcutKey);
                    return modifiersText + (modifiersText.Length > 0 && keyText.Length > 0 ? " + " : "") + keyText;
                }
                return _shortcutKeyDisplayText;
            }
            set
            {
                if (_shortcutKeyDisplayText != value)
                {
                    _shortcutKeyDisplayText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ShortcutErrorText
        {
            get => _shortcutErrorText;
            set
            {
                if (_shortcutErrorText != value)
                {
                    _shortcutErrorText = value;
                    OnPropertyChanged();
                }
            }
        }

        public Visibility ShortcutErrorVisibility
        {
            get => _shortcutErrorVisibility;
            set
            {
                if (_shortcutErrorVisibility != value)
                {
                    _shortcutErrorVisibility = value;
                    OnPropertyChanged();
                }
            }
        }

        public RelayCommand CaptureKeyCommand { get; }

        public SettingsViewModel(
            AppSettings settings, 
            IStartupService startupService,
            ILogService logService,
            IOverlayService overlayService,
            SettingsService settingsService,
            ITextSelectionService textSelectionService)
        {
            _originalSettings = settings; // DIコンテナから渡された設定を初期値として保持
            _currentSettings = (AppSettings)settings.Clone(); // AppSettings.Clone() を使用

            _startupService = startupService;
            _logService = logService;
            _overlayService = overlayService;
            _settingsService = settingsService;
            _textSelectionService = textSelectionService;
            _selectedOverlayPosition = _currentSettings.OverlayPosition;
            
            _logService.LogInfo($"SettingsViewModel初期化: AutoStart={_currentSettings.AutoStartEnabled}, ShortcutKey={_currentSettings.ShortcutKey}");

            SaveCommand = new RelayCommand(() => 
            {
                SaveSettings();
                OnRequestClose(); // ウィンドウを閉じる
            });
            CancelCommand = new RelayCommand(() => 
            {
                CancelChanges();
                OnRequestClose(); // ウィンドウを閉じる
            });
            OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
            ApplyCommand = new RelayCommand(ApplySettings);
            CaptureKeyCommand = new RelayCommand(ToggleKeyCapture);

            // 初期ショートカットキー表示テキストを生成
            ShortcutKeyDisplayText = ""; // プロパティのゲッターで自動生成されます

            _logService.LogInfo("SettingsViewModel: 初期化完了");
        }

        private void SaveSettings()
        {
            _logService.LogInfo("SettingsViewModel: 設定の保存を開始");
            try
            {
                // 現在のUIバインドされた設定(_currentSettings)を永続化する
                _settingsService.SaveSettings(_currentSettings);
                _originalSettings = (AppSettings)_currentSettings.Clone(); // 保存後、オリジナル設定も更新 (キャスト追加)
                
                // 自動起動設定の更新
                if (_originalSettings.AutoStartEnabled)
                {
                    _startupService.EnableStartup();
                }
                else
                {
                    _startupService.DisableStartup();
                }

                _textSelectionService.UpdateSettings((AppSettings)_currentSettings.Clone());
                _overlayService.UpdateSettings(); // OverlayServiceにも設定変更を通知

                _logService.LogInfo("SettingsViewModel: 設定を保存しました。");
            }
            catch (Exception ex)
            {
                _logService.LogError("設定の保存中にエラーが発生しました", ex);
                // ユーザーにエラーを通知する処理（例: MessageBox）を追加することも検討
            }
        }

        private void ApplySettings()
        {
            _logService.LogInfo("SettingsViewModel: 設定の適用を開始");
            SaveSettings(); // 設定を保存する（ウィンドウは閉じない）
            _logService.LogInfo("SettingsViewModel: 設定を適用しました。");
        }

        private void CancelChanges()
        {
            _logService.LogInfo("SettingsViewModel: 変更をキャンセルします。");
            // 現在の設定を初期読み込み時の状態に戻す
            _currentSettings = (AppSettings)_originalSettings.Clone(); // AppSettings.Clone() を使用
            _selectedOverlayPosition = _currentSettings.OverlayPosition;
            
            // UIに変更を通知 (全てのプロパティ)
            OnPropertyChanged(string.Empty); 

            // OverlayServiceにもキャンセルを通知して表示を元に戻す
            _overlayService.UpdateSettings();
            
            OnRequestClose(); // ウィンドウを閉じる
        }

        protected virtual void OnRequestClose()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void OpenLogDirectory()
        {
            try
            {
                string logPath = _logService.GetLogDirectoryPath();
                System.Diagnostics.Process.Start("explorer.exe", logPath);
            }
            catch (Exception ex)
            {
                _logService.LogError("ログディレクトリを開けませんでした", ex);
            }
        }

        private void ToggleKeyCapture()
        {
            _isCapturingKey = !_isCapturingKey;
            OnPropertyChanged(nameof(CaptureButtonText));
            
            if (_isCapturingKey)
            {
                _logService.LogInfo("キー入力キャプチャを開始しました。");
                ShortcutErrorText = "";
                ShortcutErrorVisibility = Visibility.Collapsed;

                // キーボードフックを設定
                HookManager.KeyDown += HookManager_KeyDown;
                HookManager.MouseDown += HookManager_MouseDown;
                
                // タイムアウトタイマーを設定
                _captureTimeoutTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _captureTimeoutTimer.Tick += CaptureTimeoutTimer_Tick;
                _captureTimeoutTimer.Start();
            }
            else
            {
                _logService.LogInfo("キー入力キャプチャをキャンセルしました。");
                StopCapture();
            }
        }
        
        private void StopCapture()
        {
            _isCapturingKey = false;
            OnPropertyChanged(nameof(CaptureButtonText));
            
            // キーボードフックを解除
            HookManager.KeyDown -= HookManager_KeyDown;
            HookManager.MouseDown -= HookManager_MouseDown;
            
            // タイマーを停止
            if (_captureTimeoutTimer != null)
            {
                _captureTimeoutTimer.Stop();
                _captureTimeoutTimer = null;
            }
        }
        
        private void CaptureTimeoutTimer_Tick(object? sender, EventArgs e)
        {
            _logService.LogInfo("キー入力キャプチャがタイムアウトしました。");
            ShortcutErrorText = "タイムアウト: キー入力を検出できませんでした。";
            ShortcutErrorVisibility = Visibility.Visible;
            StopCapture();
        }
        
        private void HookManager_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!_isCapturingKey) return;
            
            _logService.LogInfo($"キー入力を検出しました: {e.KeyCode}");
            
            // テスト可能な特殊キーを除外
            if (e.KeyCode == Keys.Escape || 
                e.KeyCode == Keys.Control || 
                e.KeyCode == Keys.Shift || 
                e.KeyCode == Keys.Menu)
            {
                return; // 修飾キーだけの場合は無視
            }
            
            // 他のキーの場合、キャプチャを停止してキーを設定
            e.Handled = true; // イベントを処理済みとマーク
            
            // 現在の修飾キー状態を取得
            Keys modifiers = Keys.None;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Control) == Keys.Control)
                modifiers |= Keys.Control;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                modifiers |= Keys.Shift;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                modifiers |= Keys.Alt;
            
            // 設定を更新
            _currentSettings.ShortcutKey = e.KeyCode;
            _currentSettings.ShortcutModifiers = modifiers;

            // 表示テキストを更新
            ShortcutKeyDisplayText = 
                GetModifierText(modifiers) + 
                (modifiers != Keys.None ? " + " : "") + 
                GetKeyText(e.KeyCode);

            OnPropertyChanged(nameof(ShortcutKey));
            OnPropertyChanged(nameof(ShortcutModifiers));
            
            // キャプチャを停止
            StopCapture();
        }
        
        private void HookManager_MouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isCapturingKey) return;
            
            // マウスボタン→キーコードへの変換
            Keys keyCode;
            switch (e.Button)
            {
                case System.Windows.Forms.MouseButtons.Left:
                    keyCode = Keys.LButton;
                    break;
                case System.Windows.Forms.MouseButtons.Right:
                    keyCode = Keys.RButton;
                    break;
                case System.Windows.Forms.MouseButtons.Middle:
                    keyCode = Keys.MButton;
                    break;
                case System.Windows.Forms.MouseButtons.XButton1:
                    keyCode = Keys.XButton1;
                    break;
                case System.Windows.Forms.MouseButtons.XButton2:
                    keyCode = Keys.XButton2;
                    break;
                default:
                    return; // 処理対象外
            }
            
            _logService.LogInfo($"マウス入力を検出しました: {keyCode}");
            
            // 現在の修飾キー状態を取得
            Keys modifiers = Keys.None;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Control) == Keys.Control)
                modifiers |= Keys.Control;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                modifiers |= Keys.Shift;
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                modifiers |= Keys.Alt;
            
            // 設定を更新
            _currentSettings.ShortcutKey = keyCode;
            _currentSettings.ShortcutModifiers = modifiers;

            // 表示テキストを更新
            ShortcutKeyDisplayText = 
                GetModifierText(modifiers) + 
                (modifiers != Keys.None ? " + " : "") + 
                GetKeyText(keyCode);

            OnPropertyChanged(nameof(ShortcutKey));
            OnPropertyChanged(nameof(ShortcutModifiers));
            
            // キャプチャを停止
            StopCapture();
        }
        
        private string GetModifierText(Keys modifiers)
        {
            var parts = new List<string>();
            
            if ((modifiers & Keys.Control) == Keys.Control)
                parts.Add("Ctrl");
            
            if ((modifiers & Keys.Shift) == Keys.Shift)
                parts.Add("Shift");
            
            if ((modifiers & Keys.Alt) == Keys.Alt)
                parts.Add("Alt");
            
            return string.Join(" + ", parts);
        }
        
        private string GetKeyText(Keys key)
        {
            switch (key)
            {
                case Keys.LButton: return "マウス左";
                case Keys.RButton: return "マウス右";
                case Keys.MButton: return "マウス中";
                case Keys.XButton1: return "マウスサイド1";
                case Keys.XButton2: return "マウスサイド2";
                default: return key.ToString();
            }
        }
    }
    
    // キーボードとマウスイベントをグローバルにフックするためのヘルパークラス
    public static class HookManager
    {
        #region Windows API
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
        
        // キーボードイベント
        public static event EventHandler<System.Windows.Forms.KeyEventArgs>? KeyDown;
        
        // マウスイベント
        public static event EventHandler<System.Windows.Forms.MouseEventArgs>? MouseDown;
        
        private static IntPtr _keyboardHookID = IntPtr.Zero;
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static HookProc? _keyboardProc;
        private static HookProc? _mouseProc;
        
        static HookManager()
        {
            // アプリケーションの終了時にフックを解除するためのイベント
            System.Windows.Application.Current.Exit += (s, e) => {
                UninstallHooks();
            };
            
            InstallHooks();
        }
        
        private static void InstallHooks()
        {
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;
            
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null)
                {
                    var moduleHandle = GetModuleHandle(curModule.ModuleName);
                    _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
                    _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
                }
            }
        }
        
        private static void UninstallHooks()
        {
            if (_keyboardHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookID);
                _keyboardHookID = IntPtr.Zero;
            }
            
            if (_mouseHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookID);
                _mouseHookID = IntPtr.Zero;
            }
        }
        
        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var key = (Keys)hookStruct.vkCode;
                
                KeyDown?.Invoke(null, new System.Windows.Forms.KeyEventArgs(key));
            }
            
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }
        
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                System.Windows.Forms.MouseButtons button = System.Windows.Forms.MouseButtons.None;
                
                switch ((int)wParam)
                {
                    case WM_LBUTTONDOWN:
                        button = System.Windows.Forms.MouseButtons.Left;
                        break;
                    case WM_RBUTTONDOWN:
                        button = System.Windows.Forms.MouseButtons.Right;
                        break;
                    case WM_MBUTTONDOWN:
                        button = System.Windows.Forms.MouseButtons.Middle;
                        break;
                    case WM_XBUTTONDOWN:
                        int buttonDWord = (int)hookStruct.mouseData >> 16;
                        button = buttonDWord == 1 ? 
                            System.Windows.Forms.MouseButtons.XButton1 : 
                            System.Windows.Forms.MouseButtons.XButton2;
                        break;
                }
                
                if (button != System.Windows.Forms.MouseButtons.None)
                {
                    MouseDown?.Invoke(null, new System.Windows.Forms.MouseEventArgs(button, 1, 0, 0, 0));
                }
            }
            
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }
    }
} 