using System;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Media.Playback;
using System.Threading.Tasks;
using Singleton;

namespace levyke
{
    public class MusicInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public uint TrackNumber { get; set; }
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; }
        public StorageFile File { get; set; }
    }

    // Класс для получения метаданных
    public static class MusicTagHelper
    {
        public static async Task<MusicInfo> GetMusicInfo(StorageFile file)
        {
            try
            {
                var musicProperties = await file.Properties.GetMusicPropertiesAsync();

                return new MusicInfo
                {
                    Title = !string.IsNullOrEmpty(musicProperties.Title)
                           ? musicProperties.Title
                           : System.IO.Path.GetFileNameWithoutExtension(file.Name),

                    Artist = !string.IsNullOrEmpty(musicProperties.Artist)
                            ? musicProperties.Artist
                            : "Неизвестный исполнитель",

                    Album = musicProperties.Album ?? "Неизвестный альбом",
                    TrackNumber = musicProperties.TrackNumber,
                    Duration = musicProperties.Duration,
                    FilePath = file.Path,
                    File = file
                };
            }
            catch
            {
                return new MusicInfo
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(file.Name),
                    Artist = "Неизвестный исполнитель",
                    Album = "Неизвестный альбом",
                    FilePath = file.Path,
                    File = file
                };
            }
        }
    }
    public sealed partial class PlayerPage : Page
    {
        private DispatcherTimer _positionTimer;
        private bool _userIsSeeking = false;

        public PlayerPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            mediaPlayer.SetMediaPlayer(MediaPlayerSingleton.Player);
            LoadCurrentTrackInfo();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            StartPositionTimer();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
            _positionTimer?.Stop();
        }



        private void UpdatePlayButtonState()
        {
            string content = MediaPlayerSingleton.IsPlaying ? "⏸" : "▶";
            PlayPauseButton.Content = content;
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UpdatePlayButtonState();
            });
        }

        private void StartPositionTimer()
        {
            _positionTimer?.Stop();
            _positionTimer = new DispatcherTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(500);
            _positionTimer.Tick += (s, e) =>
            {
                var session = MediaPlayerSingleton.Player.PlaybackSession;
                if (session?.NaturalDuration > TimeSpan.Zero)
                {
                    ProgressSlider.Maximum = session.NaturalDuration.TotalSeconds;
                    TotalTimeText.Text = FormatTime(session.NaturalDuration);
                    if (!_userIsSeeking)
                    {
                        ProgressSlider.Value = session.Position.TotalSeconds;
                        CurrentTimeText.Text = FormatTime(session.Position);
                    }
                }
            };
            _positionTimer.Start();
        }

        private string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            // здесь можно добавить логику переключения на предыдущий трек
            // для этого нужно будет передавать плейлист между страницами
            // надо это как нибудь потом поправить
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // здесь можно добавить логику переключения на следующий трек
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // здесь можно добавить логику повтора трека
        }

        private void ProgressSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_userIsSeeking) return;
            var session = MediaPlayerSingleton.Player.PlaybackSession;
            if (session != null && session.NaturalDuration > TimeSpan.Zero)
            {
                session.Position = TimeSpan.FromSeconds(e.NewValue);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
        private async void LoadCurrentTrackInfo()
        {
            var file = MediaPlayerSingleton.CurrentFile;
            if (file == null) return;

            try
            {
                var musicInfo = await MusicTagHelper.GetMusicInfo(file);

                FullTrackTitle.Text = musicInfo.Title;
                FullTrackArtist.Text = musicInfo.Artist;

                if (!string.IsNullOrEmpty(musicInfo.Album) && musicInfo.Album != "Неизвестный альбом")
                {
                    // Можно добавить отображение альбома, если нужно
                }

                await LoadAlbumArt(file);
                UpdatePlayButtonState();
            }
            catch (Exception ex)
            {
                try
                {
                    FullTrackTitle.Text = file.DisplayName;
                    FullTrackArtist.Text = "Локальный трек";

                    var props = await file.Properties.RetrievePropertiesAsync(
                        new[] { "System.Music.Title", "System.Music.Artist" });
                    if (props["System.Music.Title"] is string title && !string.IsNullOrWhiteSpace(title))
                        FullTrackTitle.Text = title;
                    if (props["System.Music.Artist"] is string artist && !string.IsNullOrWhiteSpace(artist))
                        FullTrackArtist.Text = artist;
                }
                catch
                {
                    FullTrackTitle.Text = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    FullTrackArtist.Text = "Неизвестный исполнитель";
                }
            }
        }
        private async Task LoadAlbumArt(StorageFile file)
        {
            try
            {
                var thumbnail = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.MusicView,
                    256,
                    Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

                if (thumbnail != null && thumbnail.Size > 0)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumbnail);
                    FullAlbumArt.Source = bitmap;
                    return;
                }
            }
            catch
            {
                    FullAlbumArt.Source = new BitmapImage(new Uri("ms-appx:///Assets/DefaultAlbum.png"));
            }
        }
    }
}