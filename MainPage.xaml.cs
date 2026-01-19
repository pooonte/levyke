using Singleton;
using System;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace levyke
{

    public sealed partial class MainPage : Page
    {
        // msc
        private ObservableCollection<StorageFile> _tracks = new ObservableCollection<StorageFile>();
        private DispatcherTimer _positionTimer;
        // msc

        // pft
        private ObservableCollection<PhotoItem> _photos = new ObservableCollection<PhotoItem>();
        // pft

        // vdo
        private ObservableCollection<VideoItem> _videos = new ObservableCollection<VideoItem>();
        // vdo

        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged; // msc
            LoadMusicFiles(); // msc
            LoadPhotoFiles(); // pft
            LoadVideoFiles(); // vdo
        }
        // msc
        private async void LoadMusicFiles()
        {
            try
            {
                var musicFolder = KnownFolders.MusicLibrary;
                var queryOptions = new QueryOptions(CommonFileQuery.OrderByTitle, new[]
                {
                    ".mp3", ".wav", ".wma", ".flac", ".m4a"
                });
                var fileQuery = musicFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await fileQuery.GetFilesAsync();
                foreach (var file in files)
                    _tracks.Add(file);
                TracksList.ItemsSource = _tracks;
            }
            catch (Exception ex)
            {
                var dialog = new Windows.UI.Popups.MessageDialog($"Ошибка: {ex.Message}");
                _ = dialog.ShowAsync();
            }
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;
            try
            {
                MediaPlayerSingleton.PlayFile(file);

                MiniTrackTitle.Text = file.DisplayName;
                MiniTrackArtist.Text = "Локальный трек";

                try
                {
                    var musicInfo = await MusicTagHelper.GetMusicInfo(file);

                    MiniTrackTitle.Text = musicInfo.Title;
                    MiniTrackArtist.Text = musicInfo.Artist;

                    if (!string.IsNullOrEmpty(musicInfo.Album) && musicInfo.Album != "Неизвестный альбом")
                    {
                        // Можно добавить отображение альбома, если нужно
                    }

                    UpdatePlayButtonState();
                }
                catch (Exception ex)
                {
                    try
                    {
                        MiniTrackTitle.Text = file.DisplayName;
                        MiniTrackArtist.Text = "Локальный трек";

                        var props = await file.Properties.RetrievePropertiesAsync(
                            new[] { "System.Music.Title", "System.Music.Artist" });
                        if (props["System.Music.Title"] is string title && !string.IsNullOrWhiteSpace(title))
                            MiniTrackTitle.Text = title;
                        if (props["System.Music.Artist"] is string artist && !string.IsNullOrWhiteSpace(artist))
                            MiniTrackArtist.Text = artist;
                    }
                    catch
                    {
                        MiniTrackTitle.Text = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                        MiniTrackArtist.Text = "Неизвестный исполнитель";
                    }
                }

                try
                {
                    var thumb = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.MusicView, 256);
                    if (thumb != null && thumb.Size > 0)
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(thumb);
                        MiniAlbumArt.Source = bitmap;
                    }
                }
                catch { }

                MiniPlayerPanel.Visibility = Visibility.Visible;
                UpdatePlayButtonState();
            }
            catch (Exception ex)
            {
                await new Windows.UI.Popups.MessageDialog($"Ошибка: {ex.Message}").ShowAsync();
            }
        }

        private void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StorageFile file)
            {
                PlayTrack(file);
            }
        }

        private void UpdatePlayButtonState()
        {
            string content = MediaPlayerSingleton.IsPlaying ? "⏸" : "▶";
            MiniPlayButton.Content = content;
        }

        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UpdatePlayButtonState();
            });
        }

        private void MiniPlayButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private void OpenPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PlayerPage));
        }

        public void Album_ItemClick(object sender, ItemClickEventArgs e) { }
        public void Artist_ItemClick(object sender, ItemClickEventArgs e) { }
        // msc

        // pft
        private async void LoadPhotoFiles()
        {
            try
            {
                var picturesFolder = KnownFolders.PicturesLibrary;
                var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, new[]
                {
                    ".jpg", ".jpeg", ".png", ".gif", ".bmp"
                });
                var fileQuery = picturesFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await fileQuery.GetFilesAsync();

                foreach (var file in files)
                {
                    var thumb = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.PicturesView,
                        150);

                    var bitmap = new BitmapImage();
                    if (thumb != null && thumb.Size > 0)
                    {
                        await bitmap.SetSourceAsync(thumb);
                    }

                    _photos.Add(new PhotoItem { File = file, Thumbnail = bitmap });
                }

                PhotoList.ItemsSource = _photos;
            }
            catch (Exception ex)
            {
                var dialog = new Windows.UI.Popups.MessageDialog($"Ошибка: {ex.Message}");
                _ = dialog.ShowAsync();
            }
        }

        private void GalleryPhoto_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PhotoItem item)
            {
                Frame.Navigate(typeof(PhotoPage), item.File);
            }
        }
        // pft

        // vdo
        private async void LoadVideoFiles()
        {

            try
            {
                var videosFolder = KnownFolders.VideosLibrary;
                var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, new[]
                {
                    ".mp4", ".avi", ".mkv", ".wmv", ".mov", ".flv", ".webm"
                });
                var fileQuery = videosFolder.CreateFileQueryWithOptions(queryOptions);
                var files = await fileQuery.GetFilesAsync();

                foreach (var file in files)
                {
                    // Пропускаем недоступные файлы (OneDrive и т.д.)
                    try
                    {
                        using (var stream = await file.OpenReadAsync()) { }
                    }
                    catch
                    {
                        continue;
                    }

                    // Получаем миниатюру
                    var thumb = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.VideosView,
                        150);

                    var bitmap = new BitmapImage();
                    if (thumb != null && thumb.Size > 0)
                    {
                        await bitmap.SetSourceAsync(thumb);
                    }

                    _videos.Add(new VideoItem { File = file, Thumbnail = bitmap });
                }

                VideoList.ItemsSource = _videos;
            }
            catch (Exception ex)
            {
                var dialog = new Windows.UI.Popups.MessageDialog($"Ошибка: {ex.Message}");
                _ = dialog.ShowAsync();
            }
        }

        private void GalleryVideo_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is VideoItem item)
            {
                Frame.Navigate(typeof(VideoPage), item.File);
            }
        }
        // vdo

        public class VideoItem
        {
            public StorageFile File { get; set; }
            public BitmapImage Thumbnail { get; set; }
        }
        // vdo

        public class PhotoItem
        {
            public StorageFile File { get; set; }
            public BitmapImage Thumbnail { get; set; }
        }
        // pft
    }
}