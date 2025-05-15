using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Collections.Generic;
using TextLength.ViewModels;
using System.Windows.Resources;
using System.IO;

namespace TextLength.Views
{
    // 設定画面ウィンドウ（言語切り替えやViewModel連携を担当）
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // ViewModelをセットし、イベントをバインド
        public void SetViewModel(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            if (viewModel != null)
            {
                viewModel.RequestClose += (sender, e) => Close();
                viewModel.LanguageChanged += ViewModel_LanguageChanged;
                
                // 初期言語を設定
                UpdateLanguageResources(viewModel.Language);
            }
        }
        
        // ViewModelから言語変更イベントを受けてリソースを切り替え
        private void ViewModel_LanguageChanged(object? sender, string language)
        {
            UpdateLanguageResources(language);
        }
        
        // 言語リソースを切り替える
        private void UpdateLanguageResources(string language)
        {
            // リソースディクショナリを変更
            ResourceDictionary resourceDict = new ResourceDictionary();
            
            try
            {
                switch (language)
                {
                    case "en-US":
                        resourceDict.Source = new Uri("/TextLength;component/Resources/EnglishStrings.xaml", UriKind.Relative);
                        break;
                    case "ja-JP":
                    default:
                        resourceDict.Source = new Uri("/TextLength;component/Resources/JapaneseStrings.xaml", UriKind.Relative);
                        break;
                }
                
                // アプリケーションレベルのリソースを更新
                var oldDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && 
                        (d.Source.OriginalString.Contains("EnglishStrings.xaml") || 
                         d.Source.OriginalString.Contains("JapaneseStrings.xaml")));
                
                if (oldDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(oldDict);
                }
                
                Application.Current.Resources.MergedDictionaries.Add(resourceDict);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"言語リソースの読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel && e.AddedItems.Count > 0)
            {
                string selectedLanguage = e.AddedItems[0].ToString() ?? "";
                UpdateLanguageResources(selectedLanguage);
            }
        }
    }
} 