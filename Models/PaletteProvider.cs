using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace levyke.Models
{
    // Services/PaletteProvider.cs
    public static class PaletteProvider
    {
        public static List<ColorPalette> GetAvailablePalettes()
        {
            return new List<ColorPalette>
        {
            new ColorPalette
            {
                Name = "Тёмная (фиолетовая)",
                AppTitle = "#FFFFFF",
                MainBackground = "#121212",
                MiniPlayerBackground = "#1E1E1E",
                PlaybackControl = "#BB86FC",
                PlaybackControlForeground = "#FFFFFF"
            },
            new ColorPalette
            {
                Name = "Тёмная (бирюзовая)",
                AppTitle = "#E0F7FA",
                MainBackground = "#001F3F",
                MiniPlayerBackground = "#003366",
                PlaybackControl = "#7FDBFF",
                PlaybackControlForeground = "#001F3F"
            },
            new ColorPalette
            {
                Name = "Светлая (мягкая)",
                AppTitle = "#333333",
                MainBackground = "#F8F9FA",
                MiniPlayerBackground = "#E9ECEF",
                PlaybackControl = "#6C757D",
                PlaybackControlForeground = "#FFFFFF"
            }
        };
        }
    }
}
