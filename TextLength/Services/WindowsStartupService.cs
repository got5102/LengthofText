using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TextLength.Services
{
    public class WindowsStartupService : IStartupService
    {
        private const string AppName = "TextLength";
        private readonly string _executablePath;

        public WindowsStartupService()
        {
            _executablePath = Assembly.GetExecutingAssembly().Location;
            if (_executablePath.EndsWith(".dll"))
            {
                _executablePath = _executablePath.Replace(".dll", ".exe");
            }
        }

        public bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    
                    object? value = key.GetValue(AppName);
                    return value != null && value.ToString() == _executablePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スタートアップ設定のチェック中にエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        public void EnableStartup()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.SetValue(AppName, _executablePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スタートアップ設定の有効化中にエラーが発生しました: {ex.Message}");
            }
        }

        public void DisableStartup()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スタートアップ設定の無効化中にエラーが発生しました: {ex.Message}");
            }
        }
    }
} 