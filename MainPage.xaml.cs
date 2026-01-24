using levyke.Models;
using Singleton;
using System;
using System.Collections.Generic;
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
        private ObservableCollection<StorageFile> _tracks = new ObservableCollection<StorageFile>();
        private DispatcherTimer _positionTimer;
        private List<ColorPalette> _palettes;

        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            LoadMusicFiles();
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

        //палитры цветов
        private void LoadThemes()
        {
            _palettes = PaletteProvider.GetAvailablePalettes();
            ThemeSelector.ItemsSource = _palettes;
            ThemeSelector.DisplayMemberPath = "Name";

            // Восстанавливаем выбор
            var savedIndex = ApplicationData.Current.LocalSettings.Values["SelectedPaletteIndex"];
            if (savedIndex != null)
            {
                int index = (int)savedIndex;
                if (index >= 0 && index < _palettes.Count)
                {
                    ThemeSelector.SelectedIndex = index;
                }
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ColorPalette selected)
            {
                PaletteService.ApplyPalette(selected);

                // Сохраняем ИНДЕКС выбранной палитры
                ApplicationData.Current.LocalSettings.Values["SelectedPaletteIndex"] = ThemeSelector.SelectedIndex;
            }
        }
    }
}