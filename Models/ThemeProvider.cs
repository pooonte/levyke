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
    }
}