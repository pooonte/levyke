using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace levyke.Models
{
    // === ColorPalette (внутренний класс) ===
    public class ColorPalette
    {
        public string Name { get; set; }
        public string MainBackground { get; set; }
        public string AppTitle { get; set; }
        public string MiniPlayerBackground { get; set; }
        public string PlaybackControl { get; set; }
        public string PlaybackControlForeground { get; set; }
    }

    // === ThemeManager (объединяет ThemeProvider и ThemeService) ===
    public static class ThemeManager
    {
        private static List<ColorPalette> _themes;

        // Статический конструктор - вызывается автоматически при первом обращении
        static ThemeManager()
        {
            InitializeThemes();
        }

        private static void InitializeThemes()
        {
            _themes = new List<ColorPalette>
            {
                new ColorPalette
                {
                    Name = "Полуночный желтый",
                    MainBackground = "#252525",
                    AppTitle = "#F3B200",
                    MiniPlayerBackground = "#696969",
                    PlaybackControl = "#696969",
                },
                new ColorPalette
                {
                    Name = "Полуночный зеленый",
                    MainBackground = "#252525",
                    AppTitle = "#77B900",
                    MiniPlayerBackground = "#696969",
                    PlaybackControl = "#696969",
                },
                new ColorPalette
                {
                    Name = "Полуночный синий",
                    MainBackground = "#252525",
                    AppTitle = "#2572EB",
                    MiniPlayerBackground = "#696969",
                    PlaybackControl = "#696969",
                },
                new ColorPalette
                {
                    Name = "Полуночный красный",
                    MainBackground = "#252525",
                    AppTitle = "#AD103C",
                    MiniPlayerBackground = "#696969",
                    PlaybackControl = "#696969",
                },
                new ColorPalette
                {
                    Name = "Ржавый металл",
                    MainBackground = "#261300",
                    AppTitle = "#632F00",
                    MiniPlayerBackground = "#543A24",
                    PlaybackControl = "#543A24",
                },
                new ColorPalette
                {
                    Name = "Элегантный красный",
                    MainBackground = "#380000",
                    AppTitle = "#B01E00",
                    MiniPlayerBackground = "#61292B",
                    PlaybackControl = "#61292B",
                },
                new ColorPalette
                {
                    Name = "Розовый страж",
                    MainBackground = "#40002E",
                    AppTitle = "#C1004F",
                    MiniPlayerBackground = "#662C58",
                    PlaybackControl = "#662C58",
                },
                new ColorPalette
                {
                    Name = "Пурпурная зависть",
                    MainBackground = "#250040",
                    AppTitle = "#7200AC",
                    MiniPlayerBackground = "#4C2C66",
                    PlaybackControl = "#4C2C66",
                },
                new ColorPalette
                {
                    Name = "Милосердие",
                    MainBackground = "#004050",
                    AppTitle = "#008287",
                    MiniPlayerBackground = "#306772",
                    PlaybackControl = "#306772",
                },
                new ColorPalette
                {
                    Name = "Сибирские луга",
                    MainBackground = "#003E00",
                    AppTitle = "#199900",
                    MiniPlayerBackground = "#2D652B",
                    PlaybackControl = "#2D652B",
                },
                new ColorPalette
                {
                    Name = "Белая ночь",
                    MainBackground = "#001940",
                    AppTitle = "#006AC1",
                    MiniPlayerBackground = "#2C4566",
                    PlaybackControl = "#2C4566",
                },
                new ColorPalette
                {
                    Name = "Безумие округа Хоуп",
                    MainBackground = "#0B2E16",
                    AppTitle = "#CAE6CD",
                    MiniPlayerBackground = "#648357",
                    PlaybackControl = "#648357",
                },
            };
        }

        // === Метод из ThemeProvider ===
        public static List<ColorPalette> GetThemes()
        {
            return _themes;
        }

        // === Метод из ThemeService ===
        public static void Apply(ColorPalette palette)
        {
            try
            {
                var resources = Application.Current.Resources;

                // Применяем цвета к ресурсам приложения
                resources["MainBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MainBackground));
                resources["MiniPlayerBackgroundBrush"] = new SolidColorBrush(HexToColor(palette.MiniPlayerBackground));
                resources["PlaybackControlBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControl));
                resources["AppTitleBrush"] = new SolidColorBrush(HexToColor(palette.AppTitle));

                // Для PlaybackControlForeground используем белый или контрастный цвет
                if (!string.IsNullOrEmpty(palette.PlaybackControlForeground))
                {
                    resources["PlaybackControlForegroundBrush"] = new SolidColorBrush(HexToColor(palette.PlaybackControlForeground));
                }
                else
                {
                    resources["PlaybackControlForegroundBrush"] = new SolidColorBrush(Colors.White);
                }

                // Отладка - выводим цвет фона
                var color = ((SolidColorBrush)resources["MainBackgroundBrush"]).Color;
                System.Diagnostics.Debug.WriteLine($"✅ Тема '{palette.Name}' применена. Фон: #{color.R:X2}{color.G:X2}{color.B:X2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка применения темы: {ex.Message}");
            }
        }

        // === Вспомогательный метод из ThemeService ===
        public static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Colors.Black;

            hex = hex.Replace("#", "");

            // Если 6 символов (без альфа-канала), добавляем FF (полная непрозрачность)
            if (hex.Length == 6)
                hex = "FF" + hex;

            try
            {
                return Color.FromArgb(
                    Convert.ToByte(hex.Substring(0, 2), 16), // A
                    Convert.ToByte(hex.Substring(2, 2), 16), // R
                    Convert.ToByte(hex.Substring(4, 2), 16), // G
                    Convert.ToByte(hex.Substring(6, 2), 16)  // B
                );
            }
            catch
            {
                return Colors.Black;
            }
        }
    }
}