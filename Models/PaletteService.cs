using levyke.Models;
using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

public static class PaletteService
{
    // Применяет палитру ко всем семантическим ресурсам
    public static void ApplyPalette(ColorPalette palette)
    {
        var r = Application.Current.Resources;

        r["AppTitleBrush"] = new SolidColorBrush(HexToColor(palette.AppTitle));
        r["MainBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MainBackground));
        r["MiniPlayerBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MiniPlayerBackground));
        r["PlaybackControlBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControl));
        r["PlaybackControlForegroundBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControlForeground));
    }

    public static Color HexToColor(string hex)
    {
        hex = hex.Replace("#", "");
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8) throw new ArgumentException("Неверный HEX");
        return Color.FromArgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            Convert.ToByte(hex.Substring(6, 2), 16)
        );
    }
}
