using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TextLength.Models; // AppSettings用
using TextLength.Services; // サービスインターフェース用

namespace TextLength.Services
{
    public class SettingsService
    {
        private readonly ITextSelectionService _textSelectionService;
        private readonly ILogService _logService;
        private readonly string _settingsFilePath;

        public SettingsService(ITextSelectionService textSelectionService, ILogService logService)
        {
            _textSelectionService = textSelectionService;
            _logService = logService;
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _logService.LogInfo($"設定ファイルパス: {_settingsFilePath}");
        }

        // 設定の保存と読み込みを処理
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                _logService.LogInfo("設定を保存しています...");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, jsonString);
                _logService.LogInfo("設定を保存しました。");
                
                // ITextSelectionServiceに設定変更を通知して更新させる
                _textSelectionService?.UpdateSettings(settings); 
                _logService.LogInfo("TextSelectionServiceの設定を更新しました。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の保存中にエラーが発生しました: {ex.Message}");
                _logService.LogError($"設定の保存エラー: {ex.Message}", ex);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                _logService.LogInfo("設定を読み込んでいます...");
                if (File.Exists(_settingsFilePath))
                {
                    string jsonString = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    if (settings != null)
                    {
                        _logService.LogInfo("設定を正常に読み込みました。");
                        return settings;
                    }
                    else
                    {
                        _logService.LogWarning("設定ファイルが空か、または不正な形式です。デフォルト設定を使用します。");
                        return new AppSettings(); // 不正な場合はデフォルト設定
                    }
                }
                else
                {
                    _logService.LogInfo("設定ファイルが見つかりません。デフォルト設定を使用し、新規作成します。");
                    var defaultSettings = new AppSettings();
                    SaveSettings(defaultSettings); // デフォルト設定で新規作成
                    return defaultSettings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定の読み込み中にエラーが発生しました: {ex.Message}");
                _logService.LogError($"設定の読み込みエラー: {ex.Message}", ex);
                return new AppSettings(); // エラー時もデフォルト設定
            }
        }
    }
} 