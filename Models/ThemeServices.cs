using Windows.UI;
using Windows.UI.Xaml.Media;
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
            var newBrush = new SolidColorBrush(HexToColor(palette.MainBackground));
            r["MainBackgroundBrush"] = newBrush;

            r["MainBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MainBackground));
            r["MiniPlayerBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MiniPlayerBackground));
            r["PlaybackControlBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControl));
            r["AppTitleBrush"] = new SolidColorBrush(HexToColor(palette.AppTitle));

            var color = ((SolidColorBrush)r["MainBackgroundBrush"]).Color;
            System.Diagnostics.Debug.WriteLine($"✅ Новый фон: #{color.R:X2}{color.G:X2}{color.B:X2}");
        }

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