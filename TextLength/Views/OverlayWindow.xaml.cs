using System;
using System.Windows;
using System.Windows.Media;
using TextLength.Models;
using System.Diagnostics;

namespace TextLength.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly AppSettings _settings;
        private System.Timers.Timer? _hideTimer;
        private readonly object _timerLock = new object();
        private bool _isClosing = false;

        public OverlayWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            
            Debug.WriteLine("オーバーレイウィンドウを初期化しています");
            
            // 設定の適用
            ApplySettings();
            
            // タイマーの設定
            lock (_timerLock)
            {
                _hideTimer = new System.Timers.Timer();
                _hideTimer.Interval = _settings.OverlayDuration * 1000; // ミリ秒に変換
                _hideTimer.AutoReset = false;
                _hideTimer.Elapsed += (s, e) =>
                {
                    SafelyInvokeOnDispatcher(() =>
                    {
                        try
                        {
                            Debug.WriteLine("表示時間経過によりオーバーレイを非表示にします");
                            if (!_isClosing) Hide();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"タイマー処理中にエラーが発生しました: {ex.Message}");
                        }
                    });
                };
            }
            
            // イベントハンドラを追加
            this.Closing += (s, e) =>
            {
                _isClosing = true;
                StopTimer();
            };
        }

        public void ApplySettings()
        {
            Debug.WriteLine("オーバーレイに設定を適用します");
            
            try
            {
                // フォントサイズの設定
                CountText.FontSize = _settings.FontSize;
                
                // 文字色の設定
                if (TryParseBrush(_settings.FontColor, out Brush? fontBrush) && fontBrush != null)
                {
                    CountText.Foreground = fontBrush;
                }
                
                // 背景色の設定
                if (TryParseBrush(_settings.BackgroundColor, out Brush? bgBrush) && bgBrush != null)
                {
                    MainBorder.Background = bgBrush;
                }
                
                // タイマーのインターバルも更新
                lock (_timerLock)
                {
                    if (_hideTimer != null)
                    {
                        _hideTimer.Interval = _settings.OverlayDuration * 1000;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定適用中にエラーが発生しました: {ex.Message}");
            }
        }

        private bool TryParseBrush(string colorString, out Brush? brush)
        {
            try
            {
                if (colorString.StartsWith("#"))
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorString);
                    brush = new SolidColorBrush(color);
                    return true;
                }
                brush = null;
                return false;
            }
            catch
            {
                brush = null;
                return false;
            }
        }

        public void ShowInfo(TextSelectionInfo info, bool resetTimer = true)
        {
            Debug.WriteLine($"文字数情報を表示します: {info.CharacterCount}字");
            
            try
            {
                // タイマーをリセット
                if (resetTimer)
                {
                    StopTimer();
                }
                
                // 位置の設定
                if (_settings.OverlayPosition == "Cursor")
                {
                    // カーソル位置からのずらし
                    Left = info.SelectionEndPoint.X + 15;
                    Top = info.SelectionEndPoint.Y + 15;
                    
                    // 画面外にはみ出す場合の調整
                    AdjustPosition();
                }
                else
                {
                    // 固定位置の場合（例：右下）
                    var screenWidth = SystemParameters.PrimaryScreenWidth;
                    var screenHeight = SystemParameters.PrimaryScreenHeight;
                    
                    Left = screenWidth - 150;
                    Top = screenHeight - 50;
                }
                
                // 表示テキストの設定
                string displayText = $"{info.CharacterCount}字";
                if (_settings.ShowWordCount)
                {
                    displayText += $" / {info.WordCount}語";
                }
                
                CountText.Text = displayText;
                
                // 表示する
                Debug.WriteLine($"オーバーレイを表示します - 位置({Left},{Top})");
                Show();
                
                // タイマーを開始
                if (resetTimer && !_isClosing)
                {
                    StartTimer();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"情報表示中にエラーが発生しました: {ex.Message}");
            }
        }

        private void StopTimer()
        {
            lock (_timerLock)
            {
                try
                {
                    _hideTimer?.Stop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"タイマー停止中にエラーが発生しました: {ex.Message}");
                }
            }
        }
        
        private void StartTimer()
        {
            lock (_timerLock)
            {
                try
                {
                    if (_hideTimer != null && !_isClosing)
                    {
                        _hideTimer.Start();
                        Debug.WriteLine($"表示タイマーを開始しました ({_settings.OverlayDuration}秒)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"タイマー開始中にエラーが発生しました: {ex.Message}");
                }
            }
        }

        private void SafelyInvokeOnDispatcher(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                try
                {
                    Dispatcher.Invoke(action);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OverlayWindow: Dispatcher呼び出しエラー: {ex.Message}");
                }
            }
        }

        private void AdjustPosition()
        {
            try
            {
                // 画面外にはみ出す場合の調整
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                // 右端調整
                if (Left + ActualWidth > screenWidth)
                {
                    Left = screenWidth - ActualWidth - 10;
                }
                
                // 下端調整
                if (Top + ActualHeight > screenHeight)
                {
                    Top = screenHeight - ActualHeight - 10;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayWindow: 位置調整エラー: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _isClosing = true;
                StopTimer();
                
                lock (_timerLock)
                {
                    if (_hideTimer != null)
                    {
                        _hideTimer.Elapsed -= OnTimerElapsed;
                        _hideTimer.Dispose();
                        _hideTimer = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayWindow: 解放処理エラー: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
        
        private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            SafelyInvokeOnDispatcher(() =>
            {
                if (!_isClosing) Hide();
            });
        }
    }
} 