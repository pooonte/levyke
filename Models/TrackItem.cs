// Models/TrackItem.cs
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Foundation;
using System;

namespace levyke.Models
{
    public class TrackItem
    {
        public StorageFile File { get; set; }
        public string Title { get; set; } = "Неизвестный трек";
        public string Artist { get; set; } = "Неизвестный исполнитель";
        public string Album { get; set; } = "Неизвестный альбом";
        public string Number { get; set; } = "";

        public static async System.Threading.Tasks.Task<TrackItem> FromFile(StorageFile file)
        {
            var item = new TrackItem { File = file };

            try
            {
                var props = await file.Properties.GetMusicPropertiesAsync().AsTask();

                item.Title = string.IsNullOrEmpty(props.Title)
                    ? file.DisplayName
                    : props.Title;

                item.Artist = string.IsNullOrEmpty(props.Artist)
                    ? "Неизвестный исполнитель"
                    : props.Artist;

                item.Album = string.IsNullOrEmpty(props.Album)
                    ? "Неизвестный альбом"
                    : props.Album;
            }
            catch
            {
                item.Title = file.DisplayName;
                item.Artist = "Неизвестный исполнитель";
                item.Album = "Неизвестный альбом";
            }

            return item;
        }
    }
}