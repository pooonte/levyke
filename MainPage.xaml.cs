using levyke.Models;
using levyke.Services;
using Singleton;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Networking.Sockets;
using Windows.Devices.Bluetooth.Rfcomm;

namespace levyke
{
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<TrackItem> _tracks = new ObservableCollection<TrackItem>();
        private ObservableCollection<ArtistItem> _artists = new ObservableCollection<ArtistItem>();
        private ObservableCollection<AlbumItem> _albums = new ObservableCollection<AlbumItem>();

        private DispatcherTimer _positionTimer;
        private int _currentTrackIndex = -1;
        private bool _userIsSeeking = false;
        private bool _wasPlayingBeforeSeek = false;

        private List<TrackItem> _currentPlaylist = new List<TrackItem>();
        private bool _isArtistViewActive = false;
        private string _currentArtistName = "";

        private List<ColorPalette> _themes;

        private bool _isLoading = false;
        private bool _isInitialized = false;


        private StreamSocket _socket;
        private DataWriter _writer;
        private DataReader _reader;

        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            MediaPlayerSingleton.Player.MediaEnded += Player_MediaEnded;

            _ = InitializeLibraryAsync();
            LoadThemes();
            this.Unloaded += (s, e) => _positionTimer?.Stop();
        }

        private async Task InitializeLibraryAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ БИБЛИОТЕКИ ===");

                bool hasCache = await MusicCacheService.HasCacheAsync();
                List<CachedTrack> cachedTracks;

                if (hasCache)
                {
                    System.Diagnostics.Debug.WriteLine("📖 Загрузка из кэша...");
                    cachedTracks = await MusicCacheService.LoadCacheAsync();
                    ShowTracksFromCache(cachedTracks);

                    System.Diagnostics.Debug.WriteLine("🔄 Фоновая проверка обновлений...");
                    _ = Task.Run(async () =>
                    {
                        var updatedTracks = await MusicCacheService.QuickCheckAsync(cachedTracks);
                        if (updatedTracks.Count != cachedTracks.Count)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                ShowTracksFromCache(updatedTracks);
                            });
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("🔍 Первый запуск, сканирование...");
                    ShowLoadingIndicator(true);

                    cachedTracks = await MusicCacheService.FullScanAsync();
                    ShowTracksFromCache(cachedTracks);
                    ShowLoadingIndicator(false);
                }

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ ЗАВЕРШЕНА ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ShowTracksFromCache(List<CachedTrack> cachedTracks)
        {
            _tracks.Clear();
            _artists.Clear();
            _albums.Clear();

            var artistDict = new Dictionary<string, List<TrackItem>>();
            var albumDict = new Dictionary<string, List<TrackItem>>();

            foreach (var cached in cachedTracks)
            {
                var track = new TrackItem
                {
                    FilePath = cached.FilePath,
                    Title = cached.Title,
                    Artist = cached.Artist,
                    Album = cached.Album,
                    Duration = TimeSpan.Parse(cached.Duration)
                };

                _tracks.Add(track);

                if (!artistDict.ContainsKey(track.Artist))
                    artistDict[track.Artist] = new List<TrackItem>();
                artistDict[track.Artist].Add(track);

                if (!albumDict.ContainsKey(track.Album))
                    albumDict[track.Album] = new List<TrackItem>();
                albumDict[track.Album].Add(track);
            }

            foreach (var kvp in artistDict.OrderBy(k => k.Key))
            {
                _artists.Add(new ArtistItem
                {
                    Name = kvp.Key,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            foreach (var kvp in albumDict
                .Where(k => k.Key != "Неизвестный альбом")
                .OrderBy(k => k.Key))
            {
                _albums.Add(new AlbumItem
                {
                    Name = kvp.Key,
                    Artist = kvp.Value.First().Artist,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            TracksList.ItemsSource = _tracks;
            ArtistsList.ItemsSource = _artists;
            AlbumsList.ItemsSource = _albums;

            System.Diagnostics.Debug.WriteLine($"📊 Показано: {_tracks.Count} треков, {_artists.Count} исполнителей, {_albums.Count} альбомов");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                return;
            }

            var results = _tracks.Where(t =>
                (t.Title?.ToLower().Contains(searchText) ?? false) ||
                (t.Artist?.ToLower().Contains(searchText) ?? false) ||
                (t.Album?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            if (results.Any())
            {
                SearchResultsList.ItemsSource = results;
                SearchResultsList.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;

                if (EmptyStatePanel.Children[1] is TextBlock emptyText)
                    emptyText.Text = "Ничего не найдено";
            }
        }

        private async void SearchResult_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    _currentPlaylist = new List<TrackItem> { track };
                    _isArtistViewActive = false;
                    PlayTrack(file);
                }
                catch
                {
                    _tracks.Remove(track);
                    SearchBox_TextChanged(null, null);
                }
            }
        }

        private async void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    _currentPlaylist = _tracks.ToList();
                    _isArtistViewActive = false;
                    PlayTrack(file);
                }
                catch
                {
                    _tracks.Remove(track);
                    UpdateArtistsAndAlbums();
                }
            }
        }

        private void Artist_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistItem artist)
            {
                var artistTracks = _tracks
                    .Where(t => t.Artist == artist.Name)
                    .OrderBy(t => t.Album)
                    .ThenBy(t => t.Title)
                    .ToList();

                int trackNumber = 1;
                foreach (var track in artistTracks)
                {
                    track.TrackNumber = trackNumber++;
                }

                _currentPlaylist = artistTracks;
                _isArtistViewActive = true;

                SelectedArtistName.Text = artist.Name;
                ArtistSongsList.ItemsSource = artistTracks;

                ArtistsList.Visibility = Visibility.Collapsed;
                ArtistSongsPanel.Visibility = Visibility.Visible;
            }
        }

        private async void ArtistSong_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);

                    int index = _currentPlaylist.IndexOf(track);
                    if (index >= 0)
                    {
                        _currentTrackIndex = index;
                        PlayTrack(file);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
                }
            }
        }

        private void BackToArtistsButton_Click(object sender, RoutedEventArgs e)
        {

            ArtistsList.Visibility = Visibility.Visible;
            ArtistSongsPanel.Visibility = Visibility.Collapsed;

            _currentPlaylist = _tracks.ToList();
            _isArtistViewActive = false;
        }

        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            var track = _tracks.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null) return;

            _currentTrackIndex = _tracks.IndexOf(track);
            track.File = file;

            MediaPlayerSingleton.PlayFile(file);

            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "0:00";
            _userIsSeeking = false;

            var saved = ApplicationData.Current.LocalSettings.Values["SavedVolume"];
            MediaPlayerSingleton.Player.Volume = saved is double v ? v : 1.0;

            MiniTrackTitle.Text = track.Title;
            MiniTrackArtist.Text = track.Artist;
            FullTrackTitle.Text = track.Title;
            FullTrackArtist.Text = track.Artist;

            try
            {
                var thumb = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.MusicView, 256);
                if (thumb != null && thumb.Size > 0)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumb);
                    MiniAlbumArt.Source = bitmap;
                    FullAlbumArt.Source = bitmap;
                }
            }
            catch { }

            MiniPlayerPanel.Visibility = Visibility.Visible;
            UpdatePlayButtonState();

            if (_positionTimer == null)
                StartPositionTimer();
        }

        private void PlayNextTrack()
        {
            if (_currentPlaylist.Count == 0 || _currentTrackIndex < 0)
            {
                if (_tracks.Count == 0) return;

                int globalNextIndex = (_currentTrackIndex + 1) % _tracks.Count;
                var globalNextTrack = _tracks[globalNextIndex];

                try
                {
                    var file = StorageFile.GetFileFromPathAsync(globalNextTrack.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int playlistNextIndex = (currentIndex + 1) % _currentPlaylist.Count;
            var playlistNextTrack = _currentPlaylist[playlistNextIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(playlistNextTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        private void Player_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => PlayNextTrack());
        }

        private void UpdatePlayButtonState()
        {
            // Для SymbolIcon используем Symbol вместо Source
            var symbol = MediaPlayerSingleton.IsPlaying ? Symbol.Pause : Symbol.Play;

            // Обновляем кнопку в полноэкранном плеере (SymbolIcon)
            if (FullPlayPauseIcon is SymbolIcon fullIcon)
            {
                fullIcon.Symbol = symbol;
            }

            // Обновляем кнопку в мини-плеере (Image)
            if (MiniPlayButton.Content is Image miniImage)
            {
                string iconName = MediaPlayerSingleton.IsPlaying ? "pause.png" : "play.png";
                var source = new BitmapImage(new Uri($"ms-appx:///Assets/{iconName}"));
                miniImage.Source = source;
            }
        }

        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UpdatePlayButtonState());
        }

        private void MiniPlayButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayerSingleton.TogglePlayPause();
            UpdatePlayButtonState();
        }

        private async void OpenPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            FullTrackTitle.Text = MiniTrackTitle.Text;
            FullTrackArtist.Text = MiniTrackArtist.Text;
            FullAlbumArt.Source = MiniAlbumArt.Source;
            VolumeSlider.Value = MediaPlayerSingleton.Player.Volume * 100;
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
            if (_currentPlaylist.Count == 0)
            {
                if (_tracks.Count == 0) return;
                _currentTrackIndex = Math.Max(0, _currentTrackIndex - 1);
                var track = _tracks[_currentTrackIndex];
                try
                {
                    var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int prevIndex = (currentIndex - 1 + _currentPlaylist.Count) % _currentPlaylist.Count;
            var prevTrack = _currentPlaylist[prevIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(prevTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка PrevButton: {ex.Message}");
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0)
            {
                if (_tracks.Count == 0) return;
                _currentTrackIndex = Math.Min(_tracks.Count - 1, _currentTrackIndex + 1);
                var track = _tracks[_currentTrackIndex];
                try
                {
                    var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                    PlayTrack(file);
                }
                catch { }
                return;
            }

            int currentIndex = _currentPlaylist.FindIndex(t => t.FilePath == _tracks[_currentTrackIndex]?.FilePath);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = (currentIndex + 1) % _currentPlaylist.Count;
            var nextTrack = _currentPlaylist[nextIndex];

            try
            {
                var file = StorageFile.GetFileFromPathAsync(nextTrack.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка NextButton: {ex.Message}");
            }
        }

        private void ProgressSlider_ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
        {
            _userIsSeeking = true;
            _wasPlayingBeforeSeek = MediaPlayerSingleton.IsPlaying;
            if (_wasPlayingBeforeSeek)
            {
                MediaPlayerSingleton.Player.Pause();
                UpdatePlayButtonState();
            }
        }

        private async void ProgressSlider_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            _userIsSeeking = false;
            var session = MediaPlayerSingleton.Player.PlaybackSession;
            if (session == null) return;

            double remainingTime = session.NaturalDuration.TotalSeconds - ProgressSlider.Value;

            if (remainingTime < 2 && ProgressSlider.Value > 0)
            {
                await Task.Run(() => PlayNextTrack());
            }
            else
            {
                session.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
                if (_wasPlayingBeforeSeek)
                {
                    MediaPlayerSingleton.Player.Play();
                    UpdatePlayButtonState();
                }
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

        private void LoadThemes()
        {
            _themes = ThemeManager.GetThemes();
            ThemeSelector.ItemsSource = _themes;
            ThemeSelector.DisplayMemberPath = "Name";

            var savedIndex = ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"];
            if (savedIndex != null)
            {
                ThemeSelector.SelectedIndex = (int)savedIndex;
                if (ThemeSelector.SelectedItem is ColorPalette selected)
                    ThemeManager.Apply(selected);
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ColorPalette selected)
            {
                ThemeManager.Apply(selected);
                ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"] = ThemeSelector.SelectedIndex;

                this.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MiniPlayerPanel.Background = (Brush)Application.Current.Resources["MiniPlayerBackgroundBrush"];
                MiniPlayButton.Background = (Brush)Application.Current.Resources["PlaybackControlBrush"];
                ThemeSelector.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MainName.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                FullPlayerOverlay.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                ProgressSlider.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                VolumeSlider.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                SearchBoxText.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                SearchBox.Background = (Brush)Application.Current.Resources["MiniPlayerBackgroundBrush"];
                SearchBox.BorderBrush = (Brush)Application.Current.Resources["PlaybackControlBrush"];
                RefreshLibraryButton.Background = (Brush)Application.Current.Resources["MiniPlayerBackgroundBrush"];
            }
        }

        private void VolumeSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            double vol = e.NewValue / 100.0;
            MediaPlayerSingleton.Player.Volume = vol;
            ApplicationData.Current.LocalSettings.Values["SavedVolume"] = vol;
        }

        private void FullPlayerOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            VolumeSlider.Value = MediaPlayerSingleton.Player.Volume * 30;
        }

        private void ShowLoadingIndicator(bool show)
        {
            if (MainPivot != null)
                MainPivot.IsEnabled = !show;
        }

        private void UpdateArtistsAndAlbums()
        {
            _artists.Clear();
            _albums.Clear();

            var artistGroups = _tracks
                .GroupBy(t => t.Artist)
                .Select(g => new ArtistItem
                {
                    Name = g.Key,
                    FirstTrack = g.First(),
                    TrackCount = g.Count()
                })
                .OrderBy(a => a.Name)
                .ToList();

            foreach (var artist in artistGroups)
                _artists.Add(artist);

            var albumGroups = _tracks
                .GroupBy(t => t.Album)
                .Where(g => g.Key != "Неизвестный альбом")
                .Select(g => new AlbumItem
                {
                    Name = g.Key,
                    Artist = g.First().Artist,
                    FirstTrack = g.First(),
                    TrackCount = g.Count()
                })
                .OrderBy(a => a.Name)
                .ToList();

            foreach (var album in albumGroups)
                _albums.Add(album);
        }

        private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                "Обновление библиотеки пересканирует все музыкальные файлы. Это может занять несколько минут. Продолжить?",
                "Обновление библиотеки");

            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Да") { Id = 0 });
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Нет") { Id = 1 });
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
                await RefreshLibraryAsync();
        }

        private async Task RefreshLibraryAsync()
        {
            try
            {
                _isLoading = true;
                ShowLoadingIndicator(true);

                System.Diagnostics.Debug.WriteLine("🔄 Принудительное обновление библиотеки...");

                var cachedTracks = await MusicCacheService.FullScanAsync();
                ShowTracksFromCache(cachedTracks);

                var completeDialog = new Windows.UI.Popups.MessageDialog(
                    $"Библиотека обновлена. Найдено {cachedTracks.Count} треков.", "Готово");
                _ = completeDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления: {ex.Message}");

                var errorDialog = new Windows.UI.Popups.MessageDialog(
                    $"Ошибка при обновлении: {ex.Message}", "Ошибка");
                _ = errorDialog.ShowAsync();
            }
            finally
            {
                _isLoading = false;
                ShowLoadingIndicator(false);
            }
        }

        //ARDUINO
        private async void ConnectToArduino()
        {
            try
            {
                string aqs = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                var devices = await DeviceInformation.FindAllAsync(aqs);

                if (devices.Count == 0)
                {
                    await new Windows.UI.Popups.MessageDialog("Устройство не найдено").ShowAsync();
                    return;
                }

                var device = devices[0];
                var service = await RfcommDeviceService.FromIdAsync(device.Id);
                if (service == null) return;

                _socket = new StreamSocket();
                await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);

                _writer = new DataWriter(_socket.OutputStream);
                _reader = new DataReader(_socket.InputStream);

                _writer.WriteString("Подключено к Windows!\n");
                await _writer.StoreAsync();

                uint size = await _reader.LoadAsync(1024);
                string response = _reader.ReadString(size);

                await new Windows.UI.Popups.MessageDialog($"Ответ: {response}").ShowAsync();
            }
            catch (Exception ex)
            {
                await new Windows.UI.Popups.MessageDialog($"Ошибка: {ex.Message}").ShowAsync();
            }
        }

        public class SimpleArduinoService
        {
            private SerialDevice _serial;

            public async Task SendToArduino(string text)
            {
                var devices = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                if (devices.Count == 0) return;

                _serial = await SerialDevice.FromIdAsync(devices[0].Id);
                _serial.BaudRate = 115200;

                var writer = new DataWriter(_serial.OutputStream);
                writer.WriteString(text + "\n");
                await writer.StoreAsync();
            }
        }

        private async void OnTrackChanged(string title, string artist)
        {
            var arduino = new SimpleArduinoService();
            await arduino.SendToArduino($"T:{title}");
            await Task.Delay(100);
            await arduino.SendToArduino($"A:{artist}");
        }
        //ARDUINO

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumItem album)
            {
                var albumTracks = _tracks
                    .Where(t => t.Album == album.Name)
                    .OrderBy(t => t.Title)
                    .ToList();

                int trackNumber = 1;
                foreach (var track in albumTracks)
                {
                    track.TrackNumber = trackNumber++;
                }

                _currentPlaylist = albumTracks;
                _isArtistViewActive = false; 

                SelectedAlbumName.Text = album.Name;
                AlbumSongsList.ItemsSource = albumTracks;

                AlbumsList.Visibility = Visibility.Collapsed;
                AlbumSongsPanel.Visibility = Visibility.Visible;
            }
        }

        private async void AlbumSong_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);

                    int index = _currentPlaylist.IndexOf(track);
                    if (index >= 0)
                    {
                        _currentTrackIndex = index;
                        PlayTrack(file);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
                }
            }
        }

        private void BackToAlbumsButton_Click(object sender, RoutedEventArgs e)
        {
            AlbumsList.Visibility = Visibility.Visible;
            AlbumSongsPanel.Visibility = Visibility.Collapsed;

            _currentPlaylist = _tracks.ToList();
            _isArtistViewActive = false;
        }
        private void OpenOnlinePage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Views.OnlinePage));
        }
    }
}