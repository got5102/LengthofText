using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TextLength.Models;
using TextLength.Services;
using TextLength.ViewModels;
using TextLength.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hardcodet.Wpf.TaskbarNotification;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Automation;
using System.Threading;
using System.Runtime.InteropServices;

namespace TextLength
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private IServiceProvider? _serviceProvider;
        private ITextSelectionService? _textSelectionService;
        private IOverlayService? _overlayService;
        private ILogService? _logService;
        private FileStream? _logFileStream;
        private AppSettings? _appSettings;
        private System.Drawing.Icon? _defaultAppIcon;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);

        public App()
        {
            // 例外ハンドラの設定
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // ログ設定
            try
            {
                string logFilePath = Path.Combine(AppContext.BaseDirectory, "debug_verbose.log");
                _logFileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                TextWriterTraceListener listener = new TextWriterTraceListener(_logFileStream);
                System.Diagnostics.Trace.Listeners.Add(listener);
                System.Diagnostics.Trace.AutoFlush = true;
                
                System.Diagnostics.Trace.WriteLine("=========================================");
                System.Diagnostics.Trace.WriteLine($"アプリケーション開始: {DateTime.Now}");
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText("startup_error.log", $"[{DateTime.Now}] ログ設定エラー: {ex.Message}\n{ex.StackTrace}");
                }
                catch { /* 何もできない場合は無視 */ }
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // DIコンテナの設定
                var services = new ServiceCollection();

                // 設定を最初に読み込み
                _appSettings = LoadAppSettingsEarly();
                services.AddSingleton(_appSettings);

                // サービスの登録
                services.AddSingleton<ILogService>(provider => 
                    new FileLogService(provider.GetRequiredService<AppSettings>())
                );

                services.AddSingleton<IUIAutomationTextService, UIAutomationTextService>();

                services.AddSingleton<ITextSelectionService>(provider => 
                    new ShortcutTextService(
                        provider.GetRequiredService<AppSettings>(), 
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IUIAutomationTextService>()
                    ));
                services.AddSingleton<IOverlayService, OverlayService>();
                services.AddSingleton<IStartupService, WindowsStartupService>();
                services.AddSingleton<SettingsService>(provider =>
                    new SettingsService(
                        provider.GetRequiredService<ITextSelectionService>(),
                        provider.GetRequiredService<ILogService>()
                    ));

                // ViewModelの登録
                services.AddTransient<SettingsViewModel>(provider => 
                    new SettingsViewModel(
                        provider.GetRequiredService<AppSettings>(),
                        provider.GetRequiredService<IStartupService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IOverlayService>(),
                        provider.GetRequiredService<SettingsService>(),
                        provider.GetRequiredService<ITextSelectionService>()
                    ));

                _serviceProvider = services.BuildServiceProvider();

                // サービスの取得
                _logService = _serviceProvider.GetRequiredService<ILogService>();
                _textSelectionService = _serviceProvider.GetRequiredService<ITextSelectionService>();
                _overlayService = _serviceProvider.GetRequiredService<IOverlayService>();
                _appSettings = _serviceProvider.GetRequiredService<AppSettings>(); 

                _logService.LogInfo("DIコンテナの設定が完了しました");

                // 設定の読み込み
                var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
                var latestSettings = settingsService.LoadSettings();
                _logService.LogInfo($"設定を読み込みました - ショートカット:{latestSettings.ShortcutKey}, 自動起動:{latestSettings.AutoStartEnabled}");

                // テキスト選択サービスの設定
                if (_textSelectionService != null)
                {
                    _textSelectionService.UpdateSettings(latestSettings); 
                    _textSelectionService.TextSelectionChanged += OnTextSelectionChanged;
                    _logService.LogInfo("テキスト選択サービスを設定しました");
                }
                else
                {
                    _logService.LogError("テキスト選択サービスの初期化に失敗しました");
                }

                // タスクトレイアイコンの作成
                InitializeNotifyIcon();

                _logService.LogInfo("アプリケーションの起動が完了しました");
            }
            catch (Exception ex)
            {
                _logService?.LogError("アプリケーション起動中にエラーが発生しました", ex);
                MessageBox.Show($"アプリケーションの起動中にエラーが発生しました: {ex.Message}", 
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private AppSettings LoadAppSettingsEarly()
        {
            AppSettings appSettings = new AppSettings(); // デフォルト値で初期化
            try
            {
                string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(settingsPath))
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();
                    configuration.GetSection("AppSettings").Bind(appSettings);
                    Debug.WriteLine("設定ファイルを読み込みました");
                }
                else
                {
                    Debug.WriteLine("設定ファイルが見つからないため、デフォルト設定を使用します");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定読み込み中にエラーが発生したため、デフォルト設定を使用します: {ex.Message}");
                appSettings = new AppSettings();
            }
            return appSettings;
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                _notifyIcon = new TaskbarIcon();

                // アイコンの読み込み
                try
                {
                    var iconUri = new Uri("pack://application:,,,/Resources/AppIcon.ico", UriKind.RelativeOrAbsolute);
                    var iconStreamInfo = Application.GetResourceStream(iconUri);
                    if (iconStreamInfo != null)
                    {
                        _defaultAppIcon = new System.Drawing.Icon(iconStreamInfo.Stream);
                        _notifyIcon.Icon = _defaultAppIcon;
                    }
                    else
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                        _defaultAppIcon = System.Drawing.SystemIcons.Application;
                    }
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"アイコン読み込みエラー: {ex.Message}", ex);
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    _defaultAppIcon = System.Drawing.SystemIcons.Application;
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"タスクトレイアイコンの初期化エラー: {ex.Message}", ex);
                return;
            }

            _notifyIcon.ToolTipText = "TextLength - 選択テキスト文字数カウント";

            // コンテキストメニューの設定
            var contextMenu = new System.Windows.Controls.ContextMenu();

            // クリップボードからテキストを取得するボタン
            var getTextItem = new System.Windows.Controls.MenuItem { Header = "クリップボードからテキスト取得" };
            getTextItem.Click += (s, e) => 
            {
                try
                {
                    string clipboardText = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clipboardText))
                    {
                        Debug.WriteLine($"クリップボードからテキスト取得: {clipboardText.Length}文字");
                        var textInfo = new TextSelectionInfo(
                            selectedText: clipboardText, 
                            selectionEndPoint: new System.Windows.Point(0, 0),
                            characterCount: clipboardText.Length,
                            wordCount: clipboardText.Split().Count(s => !string.IsNullOrWhiteSpace(s)),
                            isActive: false,
                            triggeredByShortcut: false
                        );
                        OnTextSelectionChanged(this, textInfo);
                    }
                    else
                    {
                        Debug.WriteLine("クリップボードが空です");
                        MessageBox.Show("クリップボードにテキストがありません", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"クリップボード取得エラー: {ex.Message}");
                }
            };
            contextMenu.Items.Add(getTextItem);

            var enableItem = new System.Windows.Controls.MenuItem { Header = "有効" };
            enableItem.IsChecked = true;
            enableItem.Click += (s, e) => 
            {
                enableItem.IsChecked = !enableItem.IsChecked;
                _textSelectionService?.ToggleEnabled(enableItem.IsChecked);
            };
            contextMenu.Items.Add(enableItem);

            var settingsItem = new System.Windows.Controls.MenuItem { Header = "設定..." };
            settingsItem.Click += (s, e) => OpenSettingsWindow();
            contextMenu.Items.Add(settingsItem);

            // 手動更新ボタン
            var refreshItem = new System.Windows.Controls.MenuItem {
                Header = "手動で更新",
                Command = new RelayCommand(() => {
                    Debug.WriteLine("手動更新を実行");
                    try 
                    {
                        var text = System.Windows.Clipboard.GetText();
                        var info = new TextSelectionInfo(
                            selectedText: text, 
                            selectionEndPoint: new System.Windows.Point(0, 0),
                            characterCount: text.Length,
                            wordCount: text.Split().Count(s => !string.IsNullOrWhiteSpace(s)),
                            isActive: false,
                            triggeredByShortcut: false
                        );
                        OnTextSelectionChanged(this, info);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"手動更新エラー: {ex.Message}");
                    }
                })
            };
            contextMenu.Items.Insert(1, refreshItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "終了" };
            exitItem.Click += (s, e) => Shutdown();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;

            // ダブルクリックで設定画面を開く
            _notifyIcon.DoubleClickCommand = new RelayCommand(OpenSettingsWindow);
        }

        private void OpenSettingsWindow()
        {
            if (_serviceProvider != null)
            {
                try
                {
                    _logService?.LogInfo("設定ウィンドウを開きます");
                    var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
                    var latestSettings = settingsService.LoadSettings();
                    
                    var viewModel = new SettingsViewModel(
                        latestSettings,
                        _serviceProvider.GetRequiredService<IStartupService>(),
                        _serviceProvider.GetRequiredService<ILogService>(),
                        _serviceProvider.GetRequiredService<IOverlayService>(),
                        settingsService,
                        _serviceProvider.GetRequiredService<ITextSelectionService>()
                    );

                    var settingsWindow = new SettingsWindow();
                    settingsWindow.SetViewModel(viewModel);
                    settingsWindow.ShowDialog();
                    _logService?.LogInfo("設定ウィンドウを閉じました");
                }
                catch (Exception ex)
                {
                    _logService?.LogError("設定ウィンドウの表示中にエラーが発生しました", ex);
                    MessageBox.Show("設定ウィンドウを開けませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Debug.WriteLine("ServiceProviderが初期化されていないため、設定ウィンドウを開けません");
            }
        }

        private void OnTextSelectionChanged(object? sender, TextSelectionInfo e)
        {
            if (e == null) 
            {
                return;
            }

            _logService?.LogInfo($"テキスト選択: 文字数={e.CharacterCount}, 単語数={e.WordCount}");

            if (_overlayService != null)
            {
                _overlayService.ShowOverlay(e);
            }
            else
            {
                _logService?.LogError("オーバーレイサービスが初期化されていません");
            }

            if (sender == null)
            {
                return;
            }

            if (!e.TriggeredByShortcut)
            {
                if (_notifyIcon != null && _defaultAppIcon != null && _notifyIcon.Icon?.GetHashCode() != _defaultAppIcon.GetHashCode())
                {
                    _notifyIcon.Icon = _defaultAppIcon;
                }
                return;
            }
            
            if (string.IsNullOrEmpty(e.SelectedText) || e.CharacterCount == 0)
            {
                if (_notifyIcon != null)
                {
                    if (_defaultAppIcon != null && _notifyIcon.Icon?.GetHashCode() != _defaultAppIcon.GetHashCode())
                    {
                        _notifyIcon.Icon = _defaultAppIcon;
                    }
                    _notifyIcon.ToolTipText = "TextLength - 選択テキスト文字数カウント"; 
                    UpdateContextMenu(0, 0); 
                }
                return;
            }
            
            try
            {
                _logService?.LogTextSelection(e.SelectedText, e.CharacterCount, e.WordCount);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"テキスト選択のログ記録エラー: {ex.Message}", ex);
            }
            
            string iconSymbol;
            if (e.CharacterCount > 0 && !string.IsNullOrEmpty(e.SelectedText))
            {
                iconSymbol = "✓"; // アクティブ状態
            }
            else
            {
                iconSymbol = "-"; // 非アクティブ状態
            }
            
            try
            {
                if (_notifyIcon != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _notifyIcon.ToolTipText = $"TextLength - {e.CharacterCount}字 / {e.WordCount}語";
                        UpdateContextMenu(e.CharacterCount, e.WordCount); 

                        System.Drawing.Icon? newIcon = CreateIconWithText(iconSymbol);

                        if (newIcon != null)
                        {
                            var currentIconHash = _notifyIcon.Icon?.GetHashCode().ToString() ?? "null";
                            var newIconHash = newIcon.GetHashCode().ToString();
                            
                            if (currentIconHash != newIconHash) {
                                var oldIcon = _notifyIcon.Icon; 
                                _notifyIcon.Icon = newIcon;
                            }
                            else
                            {
                                if (newIcon != _notifyIcon.Icon) 
                                {
                                    newIcon.Dispose(); 
                                }
                            }
                        }
                        else
                        {
                            if (_defaultAppIcon != null && _notifyIcon.Icon?.GetHashCode() != _defaultAppIcon.GetHashCode())
                            {
                                _notifyIcon.Icon = _defaultAppIcon;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"タスクバーアイコン更新エラー: {ex.Message}", ex);
                 Application.Current.Dispatcher.Invoke(() =>
                 {
                     if (_notifyIcon != null && _defaultAppIcon != null && _notifyIcon.Icon?.GetHashCode() != _defaultAppIcon.GetHashCode()) 
                     {
                        _notifyIcon.Icon = _defaultAppIcon;
                     }
                 });
            }
        }

        // アイコン生成処理
        private Icon CreateIconWithText(string symbol, bool isError = false)
        {
            _logService?.LogInfo($"アイコン生成: シンボル '{symbol}', エラー状態: {isError}");
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            {
                gfx.Clear(System.Drawing.Color.White);
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gfx.CompositingQuality = CompositingQuality.HighQuality;
                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float fontSize = 8f;
                if (symbol == "✓") fontSize = 9f; // チェックマークは少し大きく
                if (isError) fontSize = 9f;
                
                string stringToDisplay = symbol;
                if (isError) stringToDisplay = "X";

                Font font = new Font("Segoe UI Symbol", fontSize, System.Drawing.FontStyle.Bold);
                System.Drawing.Brush textBrush = isError ? System.Drawing.Brushes.Red : System.Drawing.Brushes.Black;

                SizeF textSize = gfx.MeasureString(stringToDisplay, font);
                float x = (bitmap.Width - textSize.Width) / 2f;
                float y = (bitmap.Height - textSize.Height) / 2f;

                if (x < 0) x = 0;
                if (y < 0) y = 0;

                gfx.DrawString(stringToDisplay, font, textBrush, x, y);
                _logService?.LogDebug($"シンボル '{stringToDisplay}' を描画しました。フォントサイズ: {fontSize}pt");
                font.Dispose();
            }

            IntPtr hIcon = IntPtr.Zero;
            Icon? createdIcon = null;
            try
            {
                hIcon = bitmap.GetHicon();
                if (hIcon == IntPtr.Zero)
                {
                    _logService?.LogError("アイコン作成中にHICONの取得に失敗しました");
                    throw new InvalidOperationException("HICONの取得に失敗しました");
                }
                using (Icon tempIcon = Icon.FromHandle(hIcon))
                {
                    createdIcon = tempIcon.Clone() as Icon;
                }
                
                if (createdIcon == null)
                {
                    _logService?.LogError("アイコンの複製に失敗しました");
                    throw new InvalidOperationException("アイコンの複製に失敗しました");
                }
                _logService?.LogInfo($"アイコンを作成しました。ハッシュ: {createdIcon.GetHashCode()}");
                return createdIcon; 
            }
            catch (Exception ex)
            {
                _logService?.LogError($"アイコン作成エラー: {ex.Message}", ex);
                createdIcon?.Dispose();
                if (_defaultAppIcon != null) 
                {
                    _logService?.LogWarning("エラーのためデフォルトアイコンを使用します");
                    return _defaultAppIcon;
                }
                _logService?.LogError("デフォルトアイコンが利用できないため、システムアイコンを使用します");
                return System.Drawing.SystemIcons.Application; 
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon); 
                    _logService?.LogDebug("HICONを解放しました");
                }
                bitmap.Dispose(); 
                _logService?.LogDebug("ビットマップを解放しました");
            }
        }
        
        private void UpdateContextMenu(int charCount, int wordCount)
        {
            if (_notifyIcon?.ContextMenu?.Items == null)
            { 
                _logService?.LogError("コンテキストメニューにアクセスできません");
                return;
            }

            var existingInfoItem = _notifyIcon.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>()
                                        .FirstOrDefault(item => item.Name == "InfoMenuItem");

            var existingSeparator = _notifyIcon.ContextMenu.Items.OfType<System.Windows.Controls.Separator>().FirstOrDefault();
            bool separatorIsCorrectlyPlaced = false;
            if (existingInfoItem != null && existingSeparator != null) {
                int infoIndex = _notifyIcon.ContextMenu.Items.IndexOf(existingInfoItem);
                int sepIndex = _notifyIcon.ContextMenu.Items.IndexOf(existingSeparator);
                if (sepIndex == infoIndex + 1) {
                    separatorIsCorrectlyPlaced = true;
                }
            }

            string newHeaderText = $"文字数: {charCount}字 / {wordCount}語";
            string tooltipText = $"TextLength - {charCount}字 / {wordCount}語";

            if (charCount == 0 && wordCount == 0 && _appSettings?.ShowCountsInContextMenu == false) 
            {
                if (existingInfoItem != null)
                {
                    _notifyIcon.ContextMenu.Items.Remove(existingInfoItem);
                    if(existingSeparator != null && separatorIsCorrectlyPlaced) {
                         _notifyIcon.ContextMenu.Items.Remove(existingSeparator);
                    }
                }
                var firstItemIsSeparator = _notifyIcon.ContextMenu.Items.Count > 0 && _notifyIcon.ContextMenu.Items[0] is System.Windows.Controls.Separator;
                if (firstItemIsSeparator && (_notifyIcon.ContextMenu.Items.Count == 1 || !(_notifyIcon.ContextMenu.Items[1] is System.Windows.Controls.MenuItem && ((System.Windows.Controls.MenuItem)_notifyIcon.ContextMenu.Items[1]).Name == "InfoMenuItem")))
                {
                    _notifyIcon.ContextMenu.Items.RemoveAt(0);
                }
                _notifyIcon.ToolTipText = "TextLength - 選択テキスト文字数カウント";
            }
            else
            {
                if (existingInfoItem != null)
                {
                    existingInfoItem.Header = newHeaderText;
                }
                else
                {
                    var infoItem = new System.Windows.Controls.MenuItem
                    {
                        Header = newHeaderText,
                        IsEnabled = false, 
                        FontWeight = FontWeights.Bold,
                        Name = "InfoMenuItem" 
                    };
                    _notifyIcon.ContextMenu.Items.Insert(0, infoItem);
                    existingInfoItem = infoItem;
                    separatorIsCorrectlyPlaced = false;
                }

                if (existingInfoItem != null && !separatorIsCorrectlyPlaced && _notifyIcon.ContextMenu.Items.Count > 1) 
                {
                    if(existingSeparator != null) _notifyIcon.ContextMenu.Items.Remove(existingSeparator);
                    
                    var newSeparator = new System.Windows.Controls.Separator();
                    int infoItemIndex = _notifyIcon.ContextMenu.Items.IndexOf(existingInfoItem);
                    if (infoItemIndex != -1 && infoItemIndex < _notifyIcon.ContextMenu.Items.Count -1) {
                         _notifyIcon.ContextMenu.Items.Insert(infoItemIndex + 1, newSeparator);
                    }
                }
                else if (existingSeparator != null && (_notifyIcon.ContextMenu.Items.Count == 1 || existingInfoItem == null || !_notifyIcon.ContextMenu.Items.Contains(existingInfoItem)))
                {
                    _notifyIcon.ContextMenu.Items.Remove(existingSeparator);
                }
                _notifyIcon.ToolTipText = tooltipText;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _textSelectionService?.Shutdown();
            _notifyIcon?.Dispose();
            _logService?.LogInfo("アプリケーションを終了します");
            Debug.WriteLine($"アプリケーション終了: {DateTime.Now}");
            Trace.Flush();
            _logFileStream?.Close();
            base.OnExit(e);
        }

        // 未処理の例外ハンドラ
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = (Exception)e.ExceptionObject;
                string errorMessage = $"未処理の例外が発生しました: {ex.Message}\n\nStackTrace: {ex.StackTrace}";
                
                Debug.WriteLine(errorMessage);
                File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] {errorMessage}\n\n");
                
                Debug.WriteLine($"[致命的エラー] {DateTime.Now}: {errorMessage}");
                Debug.WriteLine($"例外の種類: {ex.GetType().FullName}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"内部例外: {ex.InnerException.Message}");
                    Debug.WriteLine($"内部例外スタックトレース: {ex.InnerException.StackTrace}");
                }
                
                string verboseLogPath = Path.Combine(AppContext.BaseDirectory, "debug_verbose.log");
                try
                {
                    File.AppendAllText(verboseLogPath, $"[{DateTime.Now}] [致命的エラー] アプリケーションクラッシュ:\n");
                    File.AppendAllText(verboseLogPath, $"例外メッセージ: {ex.Message}\n");
                    File.AppendAllText(verboseLogPath, $"例外タイプ: {ex.GetType().FullName}\n");
                    File.AppendAllText(verboseLogPath, $"スタックトレース:\n{ex.StackTrace}\n");
                    if (ex.InnerException != null)
                    {
                        File.AppendAllText(verboseLogPath, $"内部例外: {ex.InnerException.Message}\n");
                        File.AppendAllText(verboseLogPath, $"内部例外スタックトレース:\n{ex.InnerException.StackTrace}\n");
                    }
                    File.AppendAllText(verboseLogPath, "----------------------------------------\n");
                }
                catch (Exception logEx)
                {
                    try
                    {
                        File.AppendAllText("critical_error.log", $"[{DateTime.Now}] ログ書き込みエラー: {logEx.Message}\n");
                    }
                    catch { /* 何もできない */ }
                }
                
                MessageBox.Show(errorMessage, "予期せぬエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception logEx)
            {
                try
                {
                    File.AppendAllText("critical_error.log", $"[{DateTime.Now}] 致命的なエラーの記録中にさらに例外が発生: {logEx.Message}\n\n");
                }
                catch
                {
                    // ここまで来たら何もできない
                }
            }
        }

        // UIスレッドでの例外ハンドラ
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                string errorMessage = $"UIスレッドで例外が発生しました: {e.Exception.Message}\n\nStackTrace: {e.Exception.StackTrace}";
                
                Debug.WriteLine(errorMessage);
                File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] {errorMessage}\n\n");
                
                Debug.WriteLine($"[UIエラー] {DateTime.Now}: {errorMessage}");
                Debug.WriteLine($"例外の種類: {e.Exception.GetType().FullName}");
                if (e.Exception.InnerException != null)
                {
                    Debug.WriteLine($"内部例外: {e.Exception.InnerException.Message}");
                    Debug.WriteLine($"内部例外スタックトレース: {e.Exception.InnerException.StackTrace}");
                }
                
                string verboseLogPath = Path.Combine(AppContext.BaseDirectory, "debug_verbose.log");
                try
                {
                    File.AppendAllText(verboseLogPath, $"[{DateTime.Now}] [UIエラー] UI例外発生:\n");
                    File.AppendAllText(verboseLogPath, $"例外メッセージ: {e.Exception.Message}\n");
                    File.AppendAllText(verboseLogPath, $"例外タイプ: {e.GetType().FullName}\n");
                    File.AppendAllText(verboseLogPath, $"スタックトレース:\n{e.Exception.StackTrace}\n");
                    if (e.Exception.InnerException != null)
                    {
                        File.AppendAllText(verboseLogPath, $"内部例外: {e.Exception.InnerException.Message}\n");
                        File.AppendAllText(verboseLogPath, $"内部例外スタックトレース:\n{e.Exception.InnerException.StackTrace}\n");
                    }
                    File.AppendAllText(verboseLogPath, "----------------------------------------\n");
                }
                catch (Exception logEx)
                {
                    try
                    {
                        File.AppendAllText("critical_error.log", $"[{DateTime.Now}] ログ書き込みエラー: {logEx.Message}\n");
                    }
                    catch { /* 何もできない */ }
                }
                
                MessageBox.Show(errorMessage, "UIエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 例外を処理済みとしてマーク
                e.Handled = true;
            }
            catch (Exception logEx)
            {
                try
                {
                    File.AppendAllText("critical_error.log", $"[{DateTime.Now}] UIエラーの記録中に例外が発生: {logEx.Message}\n\n");
                }
                catch
                {
                    // 何もできない
                }
            }
        }
    }

    // RelayCommandの実装
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
} 