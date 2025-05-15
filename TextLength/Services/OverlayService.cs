using System;
using System.Windows.Threading;
using TextLength.Models;
using TextLength.Views;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using System.IO;
using Hardcodet.Wpf.TaskbarNotification;

namespace TextLength.Services
{
    public class OverlayService : IOverlayService
    {
        private readonly AppSettings _settings;
        private Window? _overlayWindow;
        private readonly Dispatcher _dispatcher;
        private readonly object _overlayLock = new object();
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private TextSelectionInfo? _lastSelectionInfo;
        private DispatcherTimer? _hideTimer;
        private bool _isBusy = false;
        private int _operationId = 0;
        private readonly ILogService _logService;

        public OverlayService(AppSettings settings, ILogService logService)
        {
            _settings = settings;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _logService = logService;
            Debug.WriteLine("OverlayService: 初期化完了");
        }

        public void ShowOverlay(TextSelectionInfo selectionInfo)
        {
            try
            {
                if (selectionInfo == null)
                {
                    Debug.WriteLine("OverlayService: ShowOverlay - textInfo が null です。オーバーレイを表示せずに戻ります。");
                    return;
                }

                if (!selectionInfo.IsActive || selectionInfo.CharacterCount <= 0)
                {
                    Debug.WriteLine($"OverlayService: テキスト選択が無効 (Active={selectionInfo.IsActive}, CharCount={selectionInfo.CharacterCount})");
                    return;
                }

                Debug.WriteLine($"OverlayService: ShowOverlay - 表示位置: {selectionInfo.SelectionEndPoint}");

                // 操作IDをインクリメント
                int currentOpId = Interlocked.Increment(ref _operationId);

                // UIスレッドで実行する必要がある
                if (System.Windows.Application.Current?.Dispatcher == null)
                {
                    Debug.WriteLine("OverlayService: Applicationが存在しません。処理をスキップします。");
                    return;
                }
                
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 最新の操作でなければ無視
                        if (currentOpId != _operationId)
                        {
                            Debug.WriteLine($"OverlayService: 古い操作(ID:{currentOpId})のため無視します(現在ID:{_operationId})");
                            return;
                        }
                        
                        // 処理中なら無視
                        if (_isBusy)
                        {
                            Debug.WriteLine("OverlayService: 別の処理実行中のため無視します");
                            return;
                        }
                        
                        _isBusy = true;
                        
                        try
                        {
                            // 同一テキストの連続表示を防止（300ms以内の同一文字数は無視）
                            if ((DateTime.Now - _lastUpdateTime).TotalMilliseconds < 300 && 
                                _lastSelectionInfo?.CharacterCount == selectionInfo.CharacterCount)
                            {
                                Debug.WriteLine("OverlayService: 短時間での重複表示を防止");
                                return;
                            }
                            
                            lock(_overlayLock)
                            {
                                _lastUpdateTime = DateTime.Now;
                                _lastSelectionInfo = selectionInfo;
                                
                                Debug.WriteLine($"OverlayService: ShowOverlay呼び出し - {selectionInfo.CharacterCount}字");
                                
                                if (string.IsNullOrEmpty(selectionInfo.SelectedText))
                                {
                                    Debug.WriteLine("OverlayService: 選択テキストが空のため表示しません");
                                    return;
                                }

                                // ヒープ上の変数にウィンドウ参照を一時保存
                                Window? oldWindow = _overlayWindow;
                                _overlayWindow = null;
                                
                                // 既存のオーバーレイを閉じる
                                // 新しいウィンドウを作成する前に確実に閉じる
                                SafeCloseWindow(oldWindow);
                                oldWindow = null;
                                
                                // 処理の遅延を少し入れてウィンドウの削除を確実にする
                                Thread.Sleep(50);
                                
                                try
                                {
                                    // 新しいオーバーレイウィンドウを作成
                                    Window newWindow = CreateOverlayWindow(selectionInfo);
                                    _overlayWindow = newWindow; // 参照を割り当て
                                    
                                    // 表示位置の調整
                                    AdjustWindowPosition(_overlayWindow, selectionInfo.SelectionEndPoint);
                                    
                                    // ウィンドウを表示
                                    try
                                    {
                                        _overlayWindow.Show();
                                    }
                                    catch (Exception showEx)
                                    {
                                        Debug.WriteLine($"OverlayService: ウィンドウ表示でエラー: {showEx.Message}");
                                        _overlayWindow = null;
                                        return;
                                    }
                                    
                                    // 設定に基づいて非表示タイマーをセットアップ
                                    SetupHideTimer();
                                    
                                    Debug.WriteLine("OverlayService: オーバーレイウィンドウを表示しました");
                                }
                                catch (Exception windowEx)
                                {
                                    Debug.WriteLine($"OverlayService: ウィンドウ作成エラー: {windowEx.Message}");
                                    _overlayWindow = null;
                                }
                            }
                        }
                        finally
                        {
                            _isBusy = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _isBusy = false;
                        Debug.WriteLine($"OverlayService: UIスレッドでオーバーレイ表示中にエラーが発生しました: {ex.Message}");
                        Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                        Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Debug.WriteLine($"内部例外: {ex.InnerException.Message}");
                            Debug.WriteLine($"内部例外スタックトレース: {ex.InnerException.StackTrace}");
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: ShowOverlay メソッドでエラーが発生しました: {ex.Message}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"内部例外: {ex.InnerException.Message}");
                }
            }
        }

        private Window CreateOverlayWindow(TextSelectionInfo textInfo)
        {
            try
            {
                Debug.WriteLine("OverlayService: オーバーレイウィンドウを作成中...");
                
                var window = new Window
                {
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Topmost = true,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(Colors.Transparent),
                    SizeToContent = SizeToContent.WidthAndHeight
                };

                // 表示するコンテンツを作成
                var grid = new Grid
                {
                    Background = new SolidColorBrush(Color.FromArgb(230, 60, 60, 60)),
                    Margin = new Thickness(0)
                };

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(10)
                };

                var stackPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical
                };

                var characterCountText = new TextBlock
                {
                    Text = $"文字数: {textInfo.CharacterCount}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var wordCountText = new TextBlock
                {
                    Text = $"単語数: {textInfo.WordCount}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 14
                };

                stackPanel.Children.Add(characterCountText);
                stackPanel.Children.Add(wordCountText);
                border.Child = stackPanel;
                grid.Children.Add(border);
                window.Content = grid;

                // イベントハンドラを追加して確実にクリーンアップ
                window.Closed += (s, e) => 
                {
                    // ウィンドウが閉じられたときに確実に参照を削除
                    if (s == _overlayWindow)
                    {
                        _overlayWindow = null;
                        Debug.WriteLine("OverlayService: ウィンドウがClosedイベントでクリーンアップされました");
                    }
                };

                Debug.WriteLine("OverlayService: オーバーレイウィンドウの作成完了");
                return window;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: オーバーレイウィンドウの作成中にエラーが発生しました: {ex.Message}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                
                // 最低限のフォールバックウィンドウを返す
                try
                {
                    var fallbackWindow = new Window {
                        WindowStyle = WindowStyle.None,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false,
                        Topmost = true,
                        Width = 200,
                        Height = 100,
                        Content = new TextBlock { 
                            Text = $"文字数: {textInfo.CharacterCount}\n単語数: {textInfo.WordCount}",
                            Foreground = Brushes.Black,
                            Background = Brushes.LightGray,
                            Padding = new Thickness(10)
                        }
                    };
                    
                    // フォールバックウィンドウにもイベントハンドラを追加
                    fallbackWindow.Closed += (s, e) => 
                    {
                        if (s == _overlayWindow) _overlayWindow = null;
                    };
                    
                    return fallbackWindow;
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"OverlayService: フォールバックウィンドウの作成にも失敗: {fallbackEx.Message}");
                    throw; // 原因となる最初の例外を再スロー
                }
            }
        }

        // 安全にウィンドウを閉じる専用メソッド
        private void SafeCloseWindow(Window? window)
        {
            if (window != null)
            {
                // ウィンドウが既に閉じられているか確認
                // PresentationSource が null の場合は、ウィンドウが閉じられている可能性が高い
                if (PresentationSource.FromVisual(window) == null)
                {
                    _logService.LogDebug($"SafeCloseWindow: ウィンドウ({window.GetHashCode()})は既に閉じられているか、表示されていません。");
                    if (window == _overlayWindow) _overlayWindow = null; // 参照もクリア
                    return;
                }

                try
                {
                    _logService.LogDebug($"SafeCloseWindow: ウィンドウ({window.GetHashCode()})を閉じます。");
                    window.Close();
                }
                catch (InvalidOperationException ex)
                {
                    _logService.LogWarning($"SafeCloseWindow: ウィンドウを閉じる際にInvalidOperationException: {ex.Message}。おそらく既に閉じられています。");
                }
                catch (Exception ex)
                {
                    _logService.LogError($"SafeCloseWindow: ウィンドウを閉じる際に予期せぬエラー: {ex.Message}", ex);
                }
                finally
                {
                    if (window == _overlayWindow) _overlayWindow = null;
                }
            }
        }

        private void AdjustWindowPosition(Window window, Point screenPoint)
        {
            _logService.LogDebug($"AdjustWindowPosition: 元のスクリーン座標: ({screenPoint.X}, {screenPoint.Y})");

            if (window == null) 
            {
                _logService.LogWarning("AdjustWindowPosition: windowがnullです。位置調整をスキップします。");
                return;
            }

            try
            {
                // カーソル位置を正確に取得（引数のscreenPointより確実にするため二重チェック）
                var cursorPos = System.Windows.Forms.Cursor.Position;
                _logService.LogDebug($"AdjustWindowPosition: 現在のカーソル位置: ({cursorPos.X}, {cursorPos.Y})");
                
                // 最新のカーソル位置を優先的に使用（サブモニターなどの位置ずれを防ぐ）
                screenPoint = new Point(cursorPos.X, cursorPos.Y);
                
                // PresentationSourceの取得
                var source = PresentationSource.FromVisual(window);
                if (source == null || source.CompositionTarget == null)
                {
                    _logService.LogWarning("AdjustWindowPosition: PresentationSourceまたはCompositionTargetがnullです。デフォルト位置を使用します。");
                    window.Left = screenPoint.X;
                    window.Top = screenPoint.Y;
                    return;
                }

                // スクリーン座標 (ピクセル単位) をWPF単位 (デバイス非依存ピクセル) に変換
                Matrix transformMatrix = source.CompositionTarget.TransformFromDevice;
                Point wpfPoint = transformMatrix.Transform(screenPoint);
                _logService.LogDebug($"AdjustWindowPosition: DPI変換後のWPF座標: ({wpfPoint.X}, {wpfPoint.Y})");

                // カーソルに近い位置に表示するが、直接カーソルの上には配置しない
                // Word等でのずれを考慮して、小さめのオフセットを設定
                double newLeft = wpfPoint.X + 5; // カーソルの少し右
                double newTop = wpfPoint.Y + 5;  // カーソルの少し下

                // ウィンドウが完全に表示されるように画面境界を考慮
                // 現在のカーソルがあるスクリーンを取得
                Screen currentScreen = Screen.FromPoint(new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
                
                // Wordなどでの正確な位置表示のため、作業領域の代わりに物理的なスクリーン境界を使用
                Rect screenBounds = new Rect(
                    currentScreen.Bounds.X, 
                    currentScreen.Bounds.Y, 
                    currentScreen.Bounds.Width, 
                    currentScreen.Bounds.Height
                );
                
                _logService.LogDebug($"AdjustWindowPosition: カーソルがあるスクリーンの境界 (ピクセル単位): X={screenBounds.X}, Y={screenBounds.Y}, Width={screenBounds.Width}, Height={screenBounds.Height}");

                // スクリーン境界もWPF単位に変換
                Point screenBoundsTopLeftWpf = transformMatrix.Transform(new Point(screenBounds.X, screenBounds.Y));
                Point screenBoundsBottomRightWpf = transformMatrix.Transform(new Point(screenBounds.X + screenBounds.Width, screenBounds.Y + screenBounds.Height));
                Rect wpfScreenBounds = new Rect(screenBoundsTopLeftWpf, screenBoundsBottomRightWpf);
                _logService.LogDebug($"AdjustWindowPosition: スクリーンの境界 (WPF単位): X={wpfScreenBounds.X:F1}, Y={wpfScreenBounds.Y:F1}, Width={wpfScreenBounds.Width:F1}, Height={wpfScreenBounds.Height:F1}");

                // 作業領域もWPF単位に変換（タスクバーを考慮）
                Rect workAreaBounds = new Rect(
                    currentScreen.WorkingArea.X, 
                    currentScreen.WorkingArea.Y, 
                    currentScreen.WorkingArea.Width, 
                    currentScreen.WorkingArea.Height
                );
                Point workAreaTopLeftWpf = transformMatrix.Transform(new Point(workAreaBounds.X, workAreaBounds.Y));
                Point workAreaBottomRightWpf = transformMatrix.Transform(new Point(workAreaBounds.X + workAreaBounds.Width, workAreaBounds.Y + workAreaBounds.Height));
                Rect wpfScreenWorkingArea = new Rect(workAreaTopLeftWpf, workAreaBottomRightWpf);

                // ウィンドウのサイズを取得
                if (!window.IsLoaded)
                {
                    // まだロードされていなければ、一時的にサイズを計算させる
                    window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    window.Arrange(new Rect(window.DesiredSize));
                }
                double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
                double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
                if (windowWidth <= 0 || windowHeight <= 0)
                {
                    // SizeToContentなので、コンテンツからサイズを推定
                     window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                     windowWidth = window.DesiredSize.Width;
                     windowHeight = window.DesiredSize.Height;
                }
                _logService.LogDebug($"AdjustWindowPosition: ウィンドウサイズ (WPF単位): Width={windowWidth:F1}, Height={windowHeight:F1}");

                // まずカーソル位置を中心に考え、そこからウィンドウが表示できる方向を検討
                // デフォルトは右下
                bool placeOnLeft = false;
                bool placeOnTop = false;

                // 右側に十分なスペースがあるか
                if (wpfPoint.X + windowWidth + 5 > wpfScreenWorkingArea.Right)
                {
                    placeOnLeft = true;
                    _logService.LogDebug("AdjustWindowPosition: 右側スペース不足のため左側に配置");
                }
                
                // 下側に十分なスペースがあるか
                if (wpfPoint.Y + windowHeight + 5 > wpfScreenWorkingArea.Bottom)
                {
                    placeOnTop = true;
                    _logService.LogDebug("AdjustWindowPosition: 下側スペース不足のため上側に配置");
                }

                // 位置を決定
                if (placeOnLeft)
                {
                    newLeft = wpfPoint.X - windowWidth - 5;
                }
                else
                {
                    newLeft = wpfPoint.X + 5;
                }
                
                if (placeOnTop)
                {
                    newTop = wpfPoint.Y - windowHeight - 5;
                }
                else
                {
                    newTop = wpfPoint.Y + 5;
                }
                
                // 最終的な境界チェック
                if (newLeft < wpfScreenWorkingArea.Left) newLeft = wpfScreenWorkingArea.Left;
                if (newTop < wpfScreenWorkingArea.Top) newTop = wpfScreenWorkingArea.Top;
                if (newLeft + windowWidth > wpfScreenWorkingArea.Right) newLeft = wpfScreenWorkingArea.Right - windowWidth;
                if (newTop + windowHeight > wpfScreenWorkingArea.Bottom) newTop = wpfScreenWorkingArea.Bottom - windowHeight;

                window.Left = newLeft;
                window.Top = newTop;
                _logService.LogInfo($"AdjustWindowPosition: ウィンドウ位置を設定: Left={newLeft:F1}, Top={newTop:F1}");
            }
            catch (Exception ex)
            {
                _logService.LogError("AdjustWindowPosition: ウィンドウ位置調整中にエラーが発生しました", ex);
                // フォールバックとして単純な位置設定を試みる
                try 
                {
                    var cursorPos = System.Windows.Forms.Cursor.Position;
                    window.Left = cursorPos.X + 5;
                    window.Top = cursorPos.Y + 5;
                    _logService.LogWarning($"AdjustWindowPosition: フォールバック位置設定を使用: ({cursorPos.X + 5}, {cursorPos.Y + 5})");
                }
                catch (Exception fallbackEx)
                {
                     _logService.LogError("AdjustWindowPosition: フォールバック位置設定も失敗", fallbackEx);
                }
            }
        }

        private void SetupHideTimer()
        {
            try
            {
                // 既存のタイマーがあれば停止
                DispatcherTimer? oldTimer = _hideTimer;
                if (oldTimer != null)
                {
                    oldTimer.Stop();
                    oldTimer.Tick -= OnHideTimerTick;
                }
                
                // 新しいタイマーを作成
                _hideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_settings.OverlayDuration)
                };
                
                _hideTimer.Tick += OnHideTimerTick;
                _hideTimer.Start();
                Debug.WriteLine($"OverlayService: {_settings.OverlayDuration}秒後に非表示にするタイマーを設定");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: タイマー設定エラー: {ex.Message}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
            }
        }
        
        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            try
            {
                // タイマーを停止
                if (_hideTimer != null)
                {
                    _hideTimer.Stop();
                }
                
                Debug.WriteLine("OverlayService: 表示時間経過によるオーバーレイ閉じる処理を開始");
                
                // 現在のウィンドウ参照を安全に取得
                Window? windowToClose;
                lock(_overlayLock)
                {
                    windowToClose = _overlayWindow;
                    _overlayWindow = null;
                }
                
                if (windowToClose != null)
                {
                    // 別の操作として非同期的に実行し、ティック処理を妨げない
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // オーバーレイを閉じる
                            SafeCloseWindow(windowToClose);
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"OverlayService: 非表示タイマー内のDispatcherでエラー: {innerEx.Message}");
                            Debug.WriteLine($"例外の種類: {innerEx.GetType().FullName}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: タイマーTickイベントでエラー: {ex.Message}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
            }
        }

        private void CloseExistingOverlay()
        {
            try
            {
                Window? windowToClose;
                lock(_overlayLock)
                {
                    windowToClose = _overlayWindow;
                    _overlayWindow = null;
                }
                
                if (windowToClose != null)
                {
                    // 閉じる前にタイマーを停止
                    if (_hideTimer != null)
                    {
                        _hideTimer.Stop();
                        _hideTimer.Tick -= OnHideTimerTick;
                    }
                    
                    SafeCloseWindow(windowToClose);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: オーバーレイウィンドウを閉じる際にエラーが発生: {ex.Message}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
            }
        }

        public void HideOverlay()
        {
            try
            {
                Window? windowToHide;
                lock(_overlayLock)
                {
                    windowToHide = _overlayWindow;
                }
                
                if (windowToHide != null)
                {
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (windowToHide.IsLoaded)
                            {
                                windowToHide.Hide();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"OverlayService: オーバーレイ非表示中にエラー: {ex.Message}");
                            // エラーが発生した場合は、安全のためウィンドウを閉じる
                            SafeCloseWindow(windowToHide);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: HideOverlayメソッドでエラー: {ex.Message}");
            }
        }

        public void UpdateSettings()
        {
            try
            {
                Window? currentWindow;
                lock(_overlayLock)
                {
                    currentWindow = _overlayWindow;
                }
                
                if (currentWindow is Views.OverlayWindow overlay)
                {
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            overlay.ApplySettings();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"OverlayService: 設定更新中にエラー: {ex.Message}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OverlayService: UpdateSettingsメソッドでエラー: {ex.Message}");
            }
        }
    }
} 