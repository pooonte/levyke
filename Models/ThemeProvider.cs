using System.Collections.Generic;
using levyke.Models;

namespace levyke.Services
{
    public static class ThemeProvider
    {
        public static List<ColorPalette> GetThemes()
        {
            return new List<ColorPalette>
            {
                new ColorPalette
                {
                    Name = "Пурпурный рассвет",
                    MainBackground = "#3E2D5A",
                    AppTitle = "#529399",
                    MiniPlayerBackground = "#48607A",
                    PlaybackControl = "#57ACA7",
                    PlaybackControlForeground = "#61DFC9"
                },
                new ColorPalette
                {
                    Name = "Тёмная (бирюзовая)",
                    MainBackground = "#001F3F",
                    AppTitle = "#E0F7FA",
                    MiniPlayerBackground = "#003366",
                    PlaybackControl = "#7FDBFF",
                    PlaybackControlForeground = "#001F3F"
                },
                new ColorPalette
                {
                    Name = "Светлая",
                    MainBackground = "#FFFFFF",
                    AppTitle = "#000000",
                    MiniPlayerBackground = "#F5F5F5",
                    PlaybackControl = "#6200EE",
                    PlaybackControlForeground = "#FFFFFF"
                }
            };
        }
    }
}