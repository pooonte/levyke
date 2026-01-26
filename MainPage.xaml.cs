using levyke.Models;
using levyke.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace levyke
{
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<StorageFile> _tracks = new ObservableCollection<StorageFile>();
        private DispatcherTimer _positionTimer;
        private List<ColorPalette> _themes;
        private bool _userIsSeeking = false;

        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            LoadMusicFiles();
            LoadThemes();

            // Останавливаем таймер при закрытии страницы
            this.Unloaded += (s, e) => _positionTimer?.Stop();
        }

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
                        // Можно добавить отображение альбома
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

                // Запускаем таймер, если ещё не запущен
                if (_positionTimer == null)
                {
                    StartPositionTimer();
                }
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
            PlayPauseButton.Content = content; // Обновляем и полноэкранный плеер
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

        // === ПОЛНОЭКРАННЫЙ ПЛЕЕР ===

        private async void OpenPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            // Копируем данные из мини-плеера
            FullTrackTitle.Text = MiniTrackTitle.Text;
            FullTrackArtist.Text = MiniTrackArtist.Text;
            FullAlbumArt.Source = MiniAlbumArt.Source;

            // Обновляем кнопку
            PlayPauseButton.Content = MediaPlayerSingleton.IsPlaying ? "⏸" : "▶";

            // Показываем оверлей
            FullPlayerOverlay.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            FullPlayerOverlay.Visibility = Visibility.Collapsed;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: реализовать переход к предыдущему треку
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: реализовать переход к следующему треку
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: реализовать повтор
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

        // === ЦВЕТОВЫЕ ТЕМЫ ===

        public void Album_ItemClick(object sender, ItemClickEventArgs e) { }
        public void Artist_ItemClick(object sender, ItemClickEventArgs e) { }

        private void LoadThemes()
        {
            _themes = ThemeProvider.GetThemes();
            ThemeSelector.ItemsSource = _themes;
            ThemeSelector.DisplayMemberPath = "Name";

            var savedIndex = ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"];
            if (savedIndex != null)
            {
                ThemeSelector.SelectedIndex = (int)savedIndex;
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ColorPalette selected)
            {
                ThemeService.Apply(selected);
                ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"] = ThemeSelector.SelectedIndex;

                // Обновляем цвета вручную (на всякий случай)
                this.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MiniPlayerPanel.Background = (Brush)Application.Current.Resources["MiniPlayerBackgroundBrush"];
                MiniPlayButton.Background = (Brush)Application.Current.Resources["PlaybackControlBrush"];
                ThemeSelector.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MainName.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                FullPlayerOverlay.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                ProgressSlider.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
            }
        }
    }
}