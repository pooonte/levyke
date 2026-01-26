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
                    Name = "Кровавая роза",
                    MainBackground = "#70224C",
                    AppTitle = "#B63055",
                    MiniPlayerBackground = "#932951",
                    PlaybackControl = "#932951",
                    PlaybackControlForeground = "#A52D53"
                },
                new ColorPalette
                {
                    Name = "Тихий грибной лес",
                    MainBackground = "#3E2D5A",
                    AppTitle = "#529399",
                    MiniPlayerBackground = "#43476A",
                    PlaybackControl = "#43476A",
                    PlaybackControlForeground = "#48607A"
                },
                new ColorPalette
                {
                    Name = "Светлая гавань",
                    MainBackground = "#0E4363",
                    AppTitle = "#2199E5",
                    MiniPlayerBackground = "#253FA0",
                    PlaybackControl = "#253FA0",
                    PlaybackControlForeground = "#245DB7"
                },
                new ColorPalette
                {
                    Name = "Miku????",
                    MainBackground = "#373b3e",
                    AppTitle = "#86cecb",
                    MiniPlayerBackground = "#137a7f",
                    PlaybackControl = "#137a7f",
                    PlaybackControlForeground = "#e12885"
                },
               new ColorPalette
               {
                    Name = "Teto????",
                    MainBackground = "#3f4750",
                    AppTitle = "#ff0045",
                    MiniPlayerBackground = "#06053b",
                    PlaybackControl = "#06053b",
                    PlaybackControlForeground = "#d924d5"
               },
            };
        }
    }
}