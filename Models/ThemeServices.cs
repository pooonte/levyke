using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.Storage;
using levyke.Models;
using Windows.UI.Xaml;
using System;

namespace levyke.Services
{
    public static class ThemeService
    {
        // Применяет палитру ко всему приложению
        public static void Apply(ColorPalette palette)
        {
            var r = Application.Current.Resources;
            r["MainBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MainBackground));
            r["AppTitleBrush"] = new SolidColorBrush(HexToColor(palette.AppTitle));
            r["MiniPlayerBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MiniPlayerBackground));
            r["PlaybackControlBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControl));
            r["PlaybackControlForegroundBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControlForeground));
        }

        // Преобразует "#FF5733" → Color
        public static Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 6) hex = "FF" + hex;
            return Color.FromArgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16),
                Convert.ToByte(hex.Substring(6, 2), 16)
            );
        }
    }
}