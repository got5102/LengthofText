using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;
using TextLength.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;

namespace TextLength.Services
{
    // キーボードショートカットで選択テキストを取得・処理するサービス
    public class ShortcutTextService : ITextSelectionService
    {
        public event EventHandler<TextSelectionInfo>? TextSelectionChanged;

        private AppSettings _settings;
        private bool _isEnabled = true;
        private LowLevelKeyboardListener? _keyboardListener;
        private System.Timers.Timer _shortcutDebounceTimer;
        private bool _isShortcutProcessing = false;
        private readonly ILogService _logService;
        private readonly IUIAutomationTextService _uiAutomationTextService;

        // クリップボードデータのバックアップ用
        private System.Windows.IDataObject? _savedClipboardData = null; 

        // P/Invoke 定義
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // 仮想キーコード
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;

        private bool _isProcessingSelection = false;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public ShortcutTextService(AppSettings settings, ILogService logService, IUIAutomationTextService uiAutomationTextService)
        {
            _settings = settings;
            _logService = logService;
            _uiAutomationTextService = uiAutomationTextService;
            _logService.LogInfo("テキスト選択サービスを初期化しています");
            _logService.LogInfo($"ショートカット設定: Key={_settings.ShortcutKey}, Modifiers={_settings.ShortcutModifiers}");
            
            _shortcutDebounceTimer = new System.Timers.Timer(300);
            _shortcutDebounceTimer.AutoReset = false;
            _shortcutDebounceTimer.Elapsed += (s, e) => 
            {
                _isShortcutProcessing = false;
                _logService.LogDebug("ショートカット処理のクールダウン完了");
            };
            
            _logService.LogInfo("テキスト選択サービスの初期化が完了しました");
        }
        
        private void SetupKeyboardListener()
        {
            _logService.LogInfo("キーボードリスナーを設定しています");
            try
            {
                if (_keyboardListener != null)
                {
                    _keyboardListener.UnhookKeyboard();
                    _keyboardListener.OnKeyPressed -= KeyboardListener_OnKeyPressed;
                    _logService.LogInfo("既存のキーボードリスナーを解除しました");
                }
                _keyboardListener = new LowLevelKeyboardListener(logService: _logService);
                _keyboardListener.OnKeyPressed += KeyboardListener_OnKeyPressed;
                _keyboardListener.HookKeyboard();
                _logService.LogInfo("キーボードリスナーの設定が完了しました");
            }
            catch (Exception ex)
            {
                _logService.LogError("キーボードリスナーの初期化に失敗しました", ex);
            }
        }

        private async void KeyboardListener_OnKeyPressed(object? sender, KeyPressedEventArgs e)
        {
            if (e is null) return;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_isShortcutProcessing)
                {
                    _logService.LogDebug("すでにショートカット処理中のため、スキップします");
                    return;
                }
            
                var requiredModifiers = (Keys)_settings.ShortcutModifiers;
                var currentModifiers = System.Windows.Forms.Control.ModifierKeys;
                var pressedKey = e.KeyCode;

                _logService.LogDebug($"キー検出: {pressedKey}, 現在の修飾キー: {currentModifiers}. 設定キー: {_settings.ShortcutKey}, 設定修飾キー: {requiredModifiers}");

                if (pressedKey == _settings.ShortcutKey && 
                    (currentModifiers & requiredModifiers) == requiredModifiers && 
                    (currentModifiers != Keys.None || requiredModifiers == Keys.None))
                {
                    _logService.LogInfo($"ショートカットキーを検出しました - 設定({_settings.ShortcutModifiers}+{_settings.ShortcutKey})");
                    
                    _isShortcutProcessing = true;
                    _shortcutDebounceTimer.Stop();
                    _shortcutDebounceTimer.Start();
                    
                    await Task.Run(() => ProcessTextRequestAsync());
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("キー処理中にエラーが発生しました", ex);
                 _isShortcutProcessing = false;
            }
            finally
            {
                stopwatch.Stop();
                _logService.LogDebug($"キー処理時間: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private async Task ProcessTextRequestAsync()
        {
            if (!_isEnabled)
            {
                _logService.LogInfo("サービスが無効なため処理をスキップします");
                _isShortcutProcessing = false;
                return;
            }

            _logService.LogInfo("テキスト取得処理を開始します");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // UI Automationでのテキスト取得を試みる
                var textInfoFromUIA = await _uiAutomationTextService.GetSelectedTextInfoAsync();
                if (textInfoFromUIA != null && !string.IsNullOrEmpty(textInfoFromUIA.SelectedText))
                {
                    _logService.LogInfo("UI Automationでテキストを取得しました");
                    // UI Automationで取得したテキストを使用
                    var processedTextInfo = new TextSelectionInfo(
                        textInfoFromUIA.SelectedText,
                        textInfoFromUIA.SelectionEndPoint,
                        textInfoFromUIA.CharacterCount,
                        textInfoFromUIA.WordCount,
                        true,
                        true // ショートカットからのトリガー
                    );
                    TextSelectionChanged?.Invoke(this, processedTextInfo);
                    _isShortcutProcessing = false;
                    return;
                }

                // UI Automationが失敗した場合、クリップボード経由で再試行
                _logService.LogInfo("UI Automationでテキスト取得できず、クリップボード経由で再試行します");
                await TriggerCopyAndProcessTextAsync();
            }
            catch (Exception ex)
            {
                _logService.LogError("テキスト処理中にエラーが発生しました", ex);
                _isShortcutProcessing = false;
            }
            finally
            {
                stopwatch.Stop();
                _logService.LogDebug($"テキスト処理時間: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private async Task TriggerCopyAndProcessTextAsync()
        {
            if (!_isEnabled)
            {
                _logService.LogInfo("TriggerCopyAndProcessTextAsync: サービスが無効なため処理をスキップします。");
                _isShortcutProcessing = false;
                return;
            }

            _logService.LogInfo("TriggerCopyAndProcessTextAsync: 開始。Ctrl+C送信によるテキスト取得を試みます。");
            var stopwatch = Stopwatch.StartNew();
            IntPtr activeWindow = IntPtr.Zero;

            try
            {
                // 1. 現在のクリップボード内容を退避 (STAスレッドで実行)
                var clipboardDataSaveTask = Task.Run(() => {
                    var tcs = new TaskCompletionSource<bool>();
                    
                    Thread staThreadSave = new Thread(() =>
                    {
                        try
                        {
                            var originalDataObject = System.Windows.Clipboard.GetDataObject();
                            if (originalDataObject != null)
                            {
                                // IDataObjectを直接退避するのではなく、新しいDataObjectに内容をコピーする
                                var newDataObject = new System.Windows.DataObject();
                                string[] formats = originalDataObject.GetFormats(false); // 自動変換なし
                                _logService.LogDebug($"退避：クリップボードフォーマット数: {formats.Length}. フォーマット: {string.Join(", ", formats)}");

                                foreach (string format in formats)
                                {
                                    try
                                    {
                                        // GetDataも自動変換なしで試みる
                                        object data = originalDataObject.GetData(format, false); 
                                        if (data != null)
                                        {
                                            // SetDataも自動変換なしで試みる
                                            newDataObject.SetData(format, data, false);
                                            _logService.LogDebug($"退避：フォーマット '{format}' のデータを新しいDataObjectにコピーしました。");
                                        }
                                        else
                                        {
                                            _logService.LogDebug($"退避：フォーマット '{format}' のデータがnullでした。スキップします。");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logService.LogWarning($"退避：フォーマット '{format}' のデータ取得/設定中にエラー: {ex.Message}。このフォーマットはスキップされます。");
                                    }
                                }
                                _savedClipboardData = newDataObject; // コピーしたDataObjectを保存
                                _logService.LogInfo("退避：現在のクリップボード内容を新しいDataObjectにコピーして退避しました。");
                            }
                            else
                            {
                                _savedClipboardData = null;
                                _logService.LogDebug("退避：退避するクリップボード内容がありませんでした (GetDataObjectがnullを返しました)。");
                            }
                            tcs.SetResult(true);
                        }
                        catch (COMException comEx) 
                        {
                            _logService.LogError("退避：クリップボード内容の退避開始時にCOMエラー。", comEx);
                            _savedClipboardData = null;
                            tcs.SetException(comEx);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError("退避：クリップボード内容の退避中に一般エラー。", ex);
                            _savedClipboardData = null;
                            tcs.SetException(ex);
                        }
                    });
                    staThreadSave.SetApartmentState(ApartmentState.STA);
                    staThreadSave.Start();
                    staThreadSave.Join(); // このスレッド内での同期待機は問題ない
                    return tcs.Task;
                });
                
                await clipboardDataSaveTask;

                // 2. アクティブウィンドウにCtrl+Cを送信
                activeWindow = GetForegroundWindow();
                if (activeWindow == IntPtr.Zero)
                {
                    _logService.LogWarning("TriggerCopyAndProcessTextAsync: アクティブウィンドウが取得できませんでした。");
                    _isShortcutProcessing = false;
                    return; // 早期リターン
                }

                _logService.LogDebug($"TriggerCopyAndProcessTextAsync: アクティブウィンドウハンドル: {activeWindow}");

                // Ctrlキーを押す
                SendMessage(activeWindow, WM_KEYDOWN, (IntPtr)VK_CONTROL, IntPtr.Zero);
                await Task.Delay(50); 

                // Cキーを1回目押す
                SendMessage(activeWindow, WM_KEYDOWN, (IntPtr)VK_C, IntPtr.Zero);
                await Task.Delay(50); 
                // Cキーを1回目離す
                SendMessage(activeWindow, WM_KEYUP, (IntPtr)VK_C, IntPtr.Zero);
                await Task.Delay(50); 

                // Cキーを2回目押す
                SendMessage(activeWindow, WM_KEYDOWN, (IntPtr)VK_C, IntPtr.Zero);
                await Task.Delay(50); 
                // Cキーを2回目離す
                SendMessage(activeWindow, WM_KEYUP, (IntPtr)VK_C, IntPtr.Zero);
                await Task.Delay(50); 

                // Ctrlキーを離す
                SendMessage(activeWindow, WM_KEYUP, (IntPtr)VK_CONTROL, IntPtr.Zero);
                _logService.LogDebug("TriggerCopyAndProcessTextAsync: Ctrl+Cを2回送信しました。");

                // 3. クリップボード更新のための待機
                await Task.Delay(250); // 少し長めに待機 (150ms -> 250ms)

                // 4. クリップボードからテキストを取得 (STAスレッドで実行)
                string clipboardText = string.Empty;
                Point cursorP = new Point(0,0);

                var clipboardGetTextTask = Task.Run(() => {
                    var tcs = new TaskCompletionSource<(string Text, Point CursorPos)>();
                    
                    Thread staThreadGet = new Thread(() =>
                    {
                        try
                        {
                            string text = string.Empty;
                            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
                            {
                                text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
                            }
                            else if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text))
                            {
                                text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text);
                            }
                            
                            GetCursorPos(out POINT p);
                            var point = new Point(p.X, p.Y);

                            if (!string.IsNullOrEmpty(text))
                            {
                                _logService.LogInfo($"TriggerCopyAndProcessTextAsync: クリップボードからテキストを取得しました: '{text.Substring(0, Math.Min(text.Length, 50))}(...)'");
                            }
                            else
                            {
                                _logService.LogInfo("TriggerCopyAndProcessTextAsync: クリップボードにテキストデータが見つかりませんでした。");
                            }
                            
                            tcs.SetResult((text, point));
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError("TriggerCopyAndProcessTextAsync: クリップボードからのテキスト取得中にエラー。", ex);
                            tcs.SetException(ex);
                        }
                    });
                    staThreadGet.SetApartmentState(ApartmentState.STA);
                    staThreadGet.Start();
                    staThreadGet.Join();
                    return tcs.Task;
                });
                
                var (clipboardResult, cursorPos) = await clipboardGetTextTask;
                clipboardText = clipboardResult;
                cursorP = cursorPos;

                // 5. 取得したテキストを処理
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    ProcessSelectedText(clipboardText, cursorP, true);
                }
                else
                {
                    // テキストが取得できなかった場合も、デバウンスフラグはここで解除
                     _isShortcutProcessing = false; 
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("TriggerCopyAndProcessTextAsync で予期せぬエラーが発生しました。", ex);
                _isShortcutProcessing = false; // エラー時もデバウンス解除
            }
            finally
            {
                // 6. 元のクリップボード内容を復元 (STAスレッドで実行)
                if (_savedClipboardData != null)
                {
                    _logService.LogDebug("復元：クリップボード復元処理開始。_savedClipboardData は null ではありません。");
                    
                    var clipboardRestoreTask = Task.Run(() => {
                        var tcs = new TaskCompletionSource<bool>();
                        
                        Thread staThreadRestore = new Thread(() =>
                        {
                            try
                            {
                                _logService.LogDebug("復元 (STA)：SetDataObject 呼び出し前。");
                                // 保存したDataObjectをクリップボードに設定。第2引数copyはtrueを試す
                                System.Windows.Clipboard.SetDataObject(_savedClipboardData, true); 
                                _logService.LogInfo("復元 (STA)：元のクリップボード内容を復元しました。(copy: true)");
                                tcs.SetResult(true);
                            }
                            catch (COMException comEx)
                            {
                                _logService.LogError($"復元 (STA)：元のクリップボード内容の復元中にCOMエラー。(copy: true) ErrorCode: {comEx.ErrorCode:X}, Message: {comEx.Message}", comEx);
                                // エラー発生時、ログに保存されていたフォーマット情報を記録
                                if (_savedClipboardData != null)
                                {
                                    try
                                    {
                                        string[] formats = _savedClipboardData.GetFormats(false);
                                        _logService.LogDebug($"復元 (STA) COMエラー時の退避データフォーマット: {string.Join(", ", formats)}");
                                    }
                                    catch (Exception logEx)
                                    {
                                        _logService.LogWarning($"復元 (STA) COMエラー時のフォーマット情報取得に失敗: {logEx.Message}");
                                    }
                                }
                                tcs.SetException(comEx);
                            }
                            catch (Exception ex)
                            {
                                _logService.LogError("復元 (STA)：元のクリップボード内容の復元中に一般エラー。(copy: true)", ex);
                                tcs.SetException(ex);
                            }
                        });
                        staThreadRestore.SetApartmentState(ApartmentState.STA);
                        staThreadRestore.Start();
                        staThreadRestore.Join();
                        return tcs.Task;
                    });
                    
                    try {
                        await clipboardRestoreTask;
                    } catch (Exception ex) {
                        _logService.LogError("復元：クリップボード復元タスク中に捕捉されたエラー", ex);
                    }
                }
                else
                {
                    _logService.LogWarning("復元：_savedClipboardData が null のため、クリップボードの復元は行われませんでした。");
                }
                
                // _savedClipboardData のクリアは、復元処理が完了した後、STAスレッドの外で行う
                if (_savedClipboardData != null)
                {
                    _logService.LogDebug("TriggerCopyAndProcessTextAsync: _savedClipboardData をクリアします。");
                    _savedClipboardData = null; 
                }

                stopwatch.Stop();
                _logService.LogDebug($"TriggerCopyAndProcessTextAsync 処理時間: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private void ProcessSelectedText(string currentSelectedText, Point currentSelectionEndPoint, bool isShortcutTrigger = false)
        {
            var stopwatch = Stopwatch.StartNew();
            _logService.LogInfo("ProcessSelectedText: 選択テキスト処理開始");
            
            if (_isProcessingSelection) 
            {
                _logService.LogWarning("ProcessSelectedText: 既に処理中のためスキップします");
                return; // デバウンスは呼び出し元で行うのでここでは不要
            }
            
            _isProcessingSelection = true;
            
            try
            {
                if (string.IsNullOrEmpty(currentSelectedText))
                {
                    _logService.LogInfo("ProcessSelectedText: テキストが空のため処理をスキップ");
                    return;
                }

                // 文字列処理
                string processedText = currentSelectedText;
                if (_settings.IgnoreSpaces)
                {
                    processedText = processedText.Replace(" ", "").Replace("\t", "");
                }
                if (_settings.IgnoreLineBreaks)
                {
                    processedText = processedText.Replace("\r", "").Replace("\n", "");
                }
                
                int charCount = processedText.Length;
                int wordCount = currentSelectedText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                
                _logService.LogInfo($"ProcessSelectedText: テキスト処理完了 - 文字数={charCount}, 単語数={wordCount}");
                
                TextSelectionInfo selectionInfo = new TextSelectionInfo(
                    processedText, 
                    currentSelectionEndPoint, // UI Automationから取得した座標
                    charCount, 
                    wordCount,
                    true, // UI Automationで取得したのでアクティブな選択とみなす
                    isShortcutTrigger
                );
                
                TextSelectionChanged?.Invoke(this, selectionInfo); // UIスレッドへのディスパッチは呼び出し側(App.xaml.cs)で行う想定
            }
            catch (Exception ex)
            {
                _logService.LogError("ProcessSelectedText: テキスト処理中のエラー", ex);
            }
            finally
            {
                _isProcessingSelection = false;
                _isShortcutProcessing = false; // ここでデバウンスを解除 (成功時も失敗時も)
                stopwatch.Stop();
                _logService.LogDebug($"ProcessSelectedText 処理時間: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        public void Initialize()
        {
            _logService.LogInfo("ShortcutTextService: Initialize - 初期設定によるリスナー設定開始");
            _logService.LogInfo($"ShortcutTextService: 現在のショートカット設定 (Initialize時): Key={_settings.ShortcutKey}, Modifiers={_settings.ShortcutModifiers}");
            SetupKeyboardListener();
            _logService.LogInfo("ShortcutTextService: Initialize - 初期設定によるリスナー設定完了");
        }

        public void UpdateSettings(AppSettings newSettings)
        {
            _logService.LogInfo("ShortcutTextService: UpdateSettings - 設定更新開始");
            _logService.LogDebug($"ShortcutTextService: UpdateSettings - Old Settings: Key={_settings.ShortcutKey}, Modifiers={_settings.ShortcutModifiers}");
            _settings = newSettings; 
            _logService.LogInfo($"ShortcutTextService: 更新後のショートカット設定: Key={_settings.ShortcutKey}, Modifiers={_settings.ShortcutModifiers}");
            SetupKeyboardListener(); 
            _logService.LogInfo("ShortcutTextService: UpdateSettings - 設定更新とリスナー再設定完了");
        }

        public void Shutdown()
        {
            _logService.LogInfo("Shutdown: サービスシャットダウン開始");
            _keyboardListener?.UnhookKeyboard();
            _logService.LogInfo("Shutdown: サービスシャットダウン完了");
        }

        public TextSelectionInfo GetCurrentSelection()
        {
            // このメソッドはもはやあまり意味をなさないかもしれないが、インターフェースのために残す
            // 必要であれば、ここでもUIAutomationで現在の選択を取得するロジックを呼ぶことも可能
            _logService.LogInfo("GetCurrentSelection: 呼び出されました。新しい空のTextSelectionInfoを返します。");
            return new TextSelectionInfo(); // ダミーを返す
        }

        public void ToggleEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;
            _logService.LogInfo($"ToggleEnabled: 有効状態を {isEnabled} に変更しました。");
        }
    }
    
    // LowLevelKeyboardListener と KeyPressedEventArgs は変更なし
    public class LowLevelKeyboardListener 
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private ILogService? _logService;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event EventHandler<KeyPressedEventArgs>? OnKeyPressed;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public LowLevelKeyboardListener(ILogService? logService = null)
        {
            _proc = HookCallback;
            _logService = logService;
            _logService?.LogInfo("LowLevelKeyboardListener: 初期化");
        }

        public void HookKeyboard()
        {
            _logService?.LogInfo("LowLevelKeyboardListener: HookKeyboard開始");
            _hookID = SetHook(_proc);
            if (_hookID == IntPtr.Zero)
            {
                _logService?.LogError("LowLevelKeyboardListener: SetWindowsHookExに失敗しました", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            }
            else
            {
                _logService?.LogInfo("LowLevelKeyboardListener: HookKeyboard成功");
            }
        }

        public void UnhookKeyboard()
        {
            _logService?.LogInfo("LowLevelKeyboardListener: UnhookKeyboard開始");
            if (_hookID != IntPtr.Zero)
            {
                if (!UnhookWindowsHookEx(_hookID))
                {
                    _logService?.LogError("LowLevelKeyboardListener: UnhookWindowsHookExに失敗しました", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
                }
                else
                {
                    _logService?.LogInfo("LowLevelKeyboardListener: UnhookKeyboard成功");
                }
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            _logService?.LogDebug("LowLevelKeyboardListener: SetHook呼び出し");
            using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null)
                {
                    _logService?.LogError("LowLevelKeyboardListener: curModuleがnullです", new InvalidOperationException("MainModule is null"));
                    return IntPtr.Zero;
                }
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    OnKeyPressed?.Invoke(this, new KeyPressedEventArgs((Keys)vkCode));
                }

                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }
            catch (Exception ex)
            {
                _logService?.LogError("LowLevelKeyboardListener: HookCallbackで例外が発生しました", ex);
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
            }
        }
    }

    public class KeyPressedEventArgs : EventArgs
    {
        public Keys KeyCode { get; }

        public KeyPressedEventArgs(Keys keyCode)
        {
            KeyCode = keyCode;
        }
    }
} 