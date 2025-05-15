using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace TextLength.Converters
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return Brushes.Transparent;
            if (value is Color color)
            {
                return color.ToString();
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                // ColorをHex形式の文字列に変換
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            return "#FFFFFF"; // デフォルト値
        }
    }

    public static class ColorHelper
    {
        public static Color HexToColor(string hex)
        {
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }

            if (hex.Length == 6)
            {
                // #RRGGBB形式の場合、アルファ値を追加
                hex = "FF" + hex;
            }
            else if (hex.Length == 8)
            {
                // #AARRGGBB形式の場合はそのまま
            }
            else
            {
                throw new ArgumentException("Invalid hex color format");
            }

            return Color.FromArgb(
                byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber)
            );
        }
    }
} 