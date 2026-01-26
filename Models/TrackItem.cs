using Windows.Storage;
using System;

namespace levyke.Models
{
    public class TrackItem
    {
        public StorageFile File { get; set; }
        public string Title { get; set; } = "Неизвестный трек";
        public string Artist { get; set; } = "Неизвестный исполнитель";
        public string Album { get; set; } = "Неизвестный альбом";
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public string DurationString => FormatTime(Duration);

        private string FormatTime(TimeSpan t)
        {
            if (t.TotalSeconds <= 0) return "0:00";
            return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
        }
    }
}