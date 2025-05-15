using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Linq;
using TextLength.Models;
using System.Windows;

namespace TextLength.Services
{
    public class UIAutomationTextService : IUIAutomationTextService
    {
        private readonly ILogService _logService;
        private const int UIA_TIMEOUT_MS = 1500; // UI Automation処理のタイムアウト時間

        public UIAutomationTextService(ILogService logService)
        {
            _logService = logService;
            _logService.LogInfo("UIAutomationTextService: Initialized");
        }

        public async Task<TextSelectionInfo?> GetSelectedTextInfoAsync()
        {
            _logService.LogInfo("UIAutomationTextService: Attempting to get selected text via UI Automation.");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                return await Task.Run(() =>
                {
                    var cts = new CancellationTokenSource(UIA_TIMEOUT_MS);
                    try
                    {
                        AutomationElement focusedElement = AutomationElement.FocusedElement;
                        if (focusedElement == null)
                        {
                            _logService.LogInfo("UIA: No element has focus.");
                            return null;
                        }

                        if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
                        {
                            TextPattern textPattern = (TextPattern)patternObj;
                            TextPatternRange[] selection = textPattern.GetSelection();

                            if (selection.Length > 0)
                            {
                                string selectedText = selection[0].GetText(-1).Trim();
                                if (!string.IsNullOrEmpty(selectedText))
                                {
                                    System.Windows.Point selectionEndPoint = new System.Windows.Point(0, 0); // UIAから正確な座標取得は難しい場合がある
                                    try
                                    {
                                        Rect boundingRect = selection[0].GetBoundingRectangles().FirstOrDefault();
                                        if (boundingRect != Rect.Empty)
                                        {
                                            selectionEndPoint = new System.Windows.Point(boundingRect.Right, boundingRect.Bottom);
                                        }
                                    }
                                    catch (Exception ex) 
                                    {
                                        _logService.LogWarning($"UIA: Could not get bounding rectangle for selection: {ex.Message}");
                                    }

                                    int charCount = selectedText.Length;
                                    int wordCount = selectedText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                                    
                                    _logService.LogInfo($"UIA: Successfully retrieved text. Length: {charCount}");
                                    stopwatch.Stop();
                                    _logService.LogDebug($"UIA: Text retrieval took {stopwatch.ElapsedMilliseconds}ms.");
                                    return new TextSelectionInfo(selectedText, selectionEndPoint, charCount, wordCount, true, false);
                                }
                                _logService.LogInfo("UIA: TextPattern.GetSelection() returned text, but it was empty after trim.");
                            }
                            else
                            {
                                _logService.LogInfo("UIA: TextPattern.GetSelection() returned no ranges.");
                            }
                        }
                        else
                        {
                            _logService.LogInfo("UIA: Focused element does not support TextPattern.");
                        }
                    }
                    catch (ElementNotAvailableException ex)
                    {
                        _logService.LogWarning($"UIA: Element not available. {ex.Message}");
                    }
                    catch (InvalidOperationException ex) // 例: 要素が応答しない、または保護されている
                    {
                         _logService.LogWarning($"UIA: Invalid operation during text retrieval. {ex.Message}");
                    }
                    catch (Exception ex) // その他の予期せぬ例外
                    {
                        _logService.LogError("UIA: Unexpected error during text retrieval.", ex);
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                    return null;
                }).WaitAsync(new CancellationTokenSource(UIA_TIMEOUT_MS + 200).Token); // Task.Run自体にもタイムアウト
            }
            catch (TimeoutException)
            {
                _logService.LogWarning($"UIA: Text retrieval process timed out after {UIA_TIMEOUT_MS + 200}ms (overall).");
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError("UIA: Error in GetSelectedTextInfoAsync outer task.", ex);
                return null;
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > UIA_TIMEOUT_MS)
                   _logService.LogDebug($"UIA: Text retrieval (including timeout or error) took {stopwatch.ElapsedMilliseconds}ms.");
            }
        }
    }
} 