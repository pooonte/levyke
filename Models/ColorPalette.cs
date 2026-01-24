using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace levyke.Models
{
    public class ColorPalette
    {
        public string Name { get; set; }
        // Семантические цвета — именно так, как в ресурсах
        public string AppTitle { get; set; }               // "#FFFFFF"
        public string MainBackground { get; set; }         // "#121212"
        public string MiniPlayerBackground { get; set; }   // "#1E1E1E"
        public string PlaybackControl { get; set; }        // "#BB86FC"
        public string PlaybackControlForeground { get; set; } // "#000000"
    }
}
