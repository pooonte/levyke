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
        // UI коллекции
        private ObservableCollection<TrackItem> _tracks = new ObservableCollection<TrackItem>();
        private ObservableCollection<ArtistItem> _artists = new ObservableCollection<ArtistItem>();
        private ObservableCollection<AlbumItem> _albums = new ObservableCollection<AlbumItem>();

        // Плеер
        private DispatcherTimer _positionTimer;
        private int _currentTrackIndex = -1;
        private bool _userIsSeeking = false;

        // Темы
        private List<ColorPalette> _themes;

        // Состояние загрузки
        private bool _isLoading = false;
        private bool _isInitialized = false; // Флаг, что библиотека загружена

        // Arduino
        private StreamSocket _socket;
        private DataWriter _writer;
        private DataReader _reader;

        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            // Запускаем инициализацию библиотеки
            _ = InitializeLibraryAsync();

            LoadThemes();
            this.Unloaded += (s, e) => _positionTimer?.Stop();
        }

        // === ОСНОВНОЙ МЕТОД ИНИЦИАЛИЗАЦИИ ===
        private async Task InitializeLibraryAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== ИНИЦИАЛИЗАЦИЯ БИБЛИОТЕКИ ===");

                // 1. Проверяем наличие кэша
                bool hasCache = await MusicCacheService.HasCacheAsync();

                List<CachedTrack> cachedTracks;

                if (hasCache)
                {
                    // 2. БЫСТРО загружаем из кэша
                    System.Diagnostics.Debug.WriteLine("📖 Загрузка из кэша...");
                    cachedTracks = await MusicCacheService.LoadCacheAsync();

                    // 3. СРАЗУ показываем пользователю
                    ShowTracksFromCache(cachedTracks);

                    // 4. В ФОНЕ проверяем новые файлы
                    System.Diagnostics.Debug.WriteLine("🔄 Фоновая проверка обновлений...");

                    // Не ждём эту задачу
                    _ = Task.Run(async () =>
                    {
                        var updatedTracks = await MusicCacheService.QuickCheckAsync(cachedTracks);

                        // Если есть изменения - обновляем UI
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
                    // 5. ПЕРВЫЙ ЗАПУСК - полное сканирование
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

        // === ПОКАЗ ТРЕКОВ ИЗ КЭША ===
        private void ShowTracksFromCache(List<CachedTrack> cachedTracks)
        {
            // Очищаем текущие коллекции
            _tracks.Clear();
            _artists.Clear();
            _albums.Clear();

            // Временные множества для группировки
            var artistDict = new Dictionary<string, List<TrackItem>>();
            var albumDict = new Dictionary<string, List<TrackItem>>();

            // Создаём TrackItem из кэша
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

                // Пытаемся восстановить StorageFile позже, при воспроизведении

                _tracks.Add(track);

                // Группируем для ArtistItem и AlbumItem
                if (!artistDict.ContainsKey(track.Artist))
                    artistDict[track.Artist] = new List<TrackItem>();
                artistDict[track.Artist].Add(track);

                if (!albumDict.ContainsKey(track.Album))
                    albumDict[track.Album] = new List<TrackItem>();
                albumDict[track.Album].Add(track);
            }

            // Создаём ArtistItem
            foreach (var kvp in artistDict.OrderBy(k => k.Key))
            {
                _artists.Add(new ArtistItem
                {
                    Name = kvp.Key,
                    FirstTrack = kvp.Value.First(),
                    TrackCount = kvp.Value.Count
                });
            }

            // Создаём AlbumItem (исключая "Неизвестный альбом")
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

            // Обновляем UI
            TracksList.ItemsSource = _tracks;
            ArtistsList.ItemsSource = _artists;
            AlbumsList.ItemsSource = _albums;

            System.Diagnostics.Debug.WriteLine($"📊 Показано: {_tracks.Count} треков, {_artists.Count} исполнителей, {_albums.Count} альбомов");
        }

        // === КНОПКА ОБНОВЛЕНИЯ (добавь в XAML при необходимости) ===
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var dialog = new Windows.UI.Popups.MessageDialog(
                "Обновление пересканирует все файлы. Это может занять некоторое время. Продолжить?",
                "Обновление библиотеки");

            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Да") { Id = 0 });
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Нет") { Id = 1 });

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                ShowLoadingIndicator(true);
                var tracks = await MusicCacheService.FullScanAsync();
                ShowTracksFromCache(tracks);
                ShowLoadingIndicator(false);
            }
        }

        // === ВСПОМОГАТЕЛЬНЫЙ МЕТОД ===
        private void ShowLoadingIndicator(bool show)
        {
            if (MainPivot != null)
                MainPivot.IsEnabled = !show;
        }

        // === PlayTrack (с восстановлением StorageFile) ===
        private async void PlayTrack(StorageFile file)
        {
            if (file == null) return;

            var track = _tracks.FirstOrDefault(t => t.FilePath == file.Path);
            if (track == null) return;

            _currentTrackIndex = _tracks.IndexOf(track);
            track.File = file; // Сохраняем StorageFile

            MediaPlayerSingleton.PlayFile(file);

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

        // === Track_ItemClick с восстановлением файла ===
        private async void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    // Пытаемся получить файл по сохранённому пути
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    PlayTrack(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Не удалось открыть файл: {ex.Message}");

                    // Если файл не найден, удаляем его из коллекции
                    _tracks.Remove(track);

                    // Пересобираем исполнителей и альбомы
                    UpdateArtistsAndAlbums();
                }
            }
        }

        private void Artist_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ArtistItem artist)
            {
                PlayTrack(artist.FirstTrack.File);
            }
        }

        private void Album_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AlbumItem album)
            {
                PlayTrack(album.FirstTrack.File);
            }
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

        // === ВСЕ ОСТАЛЬНЫЕ МЕТОДЫ БЕЗ ИЗМЕНЕНИЙ ===
        private void UpdatePlayButtonState()
        {
            string iconName = MediaPlayerSingleton.IsPlaying ? "pause.png" : "play.png";
            var source = new BitmapImage(new Uri($"ms-appx:///Assets/{iconName}"));

            if (MiniPlayButton.Content is Image miniImage)
            {
                miniImage.Source = source;
            }

            FullPlayPauseIcon.Source = source;
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
            if (_tracks.Count == 0 || _currentTrackIndex < 0) return;
            _currentTrackIndex = Math.Max(0, _currentTrackIndex - 1);

            var track = _tracks[_currentTrackIndex];
            try
            {
                var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch { }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tracks.Count == 0 || _currentTrackIndex < 0) return;
            _currentTrackIndex = Math.Min(_tracks.Count - 1, _currentTrackIndex + 1);

            var track = _tracks[_currentTrackIndex];
            try
            {
                var file = StorageFile.GetFileFromPathAsync(track.FilePath).AsTask().Result;
                PlayTrack(file);
            }
            catch { }
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

        private void LoadThemes()
        {
            _themes = ThemeManager.GetThemes(); // Вместо ThemeProvider.GetThemes()
            ThemeSelector.ItemsSource = _themes;
            ThemeSelector.DisplayMemberPath = "Name";

            var savedIndex = ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"];
            if (savedIndex != null)
            {
                ThemeSelector.SelectedIndex = (int)savedIndex;

                // Применяем сохранённую тему сразу при загрузке
                if (ThemeSelector.SelectedItem is ColorPalette selected)
                {
                    ThemeManager.Apply(selected);
                }
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ColorPalette selected)
            {
                ThemeManager.Apply(selected); // Вместо ThemeService.Apply()
                ApplicationData.Current.LocalSettings.Values["SelectedThemeIndex"] = ThemeSelector.SelectedIndex;

                // Обновляем цвета элементов интерфейса
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
                if (service == null) { /* ошибка */ return; }

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

        private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            // Подтверждение действия
            var dialog = new Windows.UI.Popups.MessageDialog(
                "Обновление библиотеки пересканирует все музыкальные файлы. Это может занять несколько минут. Продолжить?",
                "Обновление библиотеки");

            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Да") { Id = 0 });
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Нет") { Id = 1 });
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                await RefreshLibraryAsync();
            }
        }
        //обновление библиотеки по кнопке
        private async Task RefreshLibraryAsync()
        {
            try
            {
                _isLoading = true;
                ShowLoadingIndicator(true);

                System.Diagnostics.Debug.WriteLine("🔄 Принудительное обновление библиотеки...");

                // Полное сканирование с игнорированием кэша
                var cachedTracks = await MusicCacheService.FullScanAsync();

                // Обновляем UI
                ShowTracksFromCache(cachedTracks);

                System.Diagnostics.Debug.WriteLine("✅ Библиотека обновлена");

                // Уведомление пользователя
                var completeDialog = new Windows.UI.Popups.MessageDialog(
                    $"Библиотека обновлена. Найдено {cachedTracks.Count} треков.",
                    "Готово");
                _ = completeDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления: {ex.Message}");

                var errorDialog = new Windows.UI.Popups.MessageDialog(
                    $"Ошибка при обновлении: {ex.Message}",
                    "Ошибка");
                _ = errorDialog.ShowAsync();
            }
            finally
            {
                _isLoading = false;
                ShowLoadingIndicator(false);
            }
        }
        // ==================== ПОИСК ====================

        /// <summary>
        /// Результаты поиска
        /// </summary>
        private ObservableCollection<TrackItem> _searchResults = new ObservableCollection<TrackItem>();

        /// <summary>
        /// Обработчик изменения текста поиска
        /// </summary>
        /// <summary>
        /// Обработчик изменения текста поиска
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Нет текста - показываем пустое состояние, скрываем список
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                return;
            }

            // Поиск по трекам
            var results = _tracks.Where(t =>
                (t.Title?.ToLower().Contains(searchText) ?? false) ||
                (t.Artist?.ToLower().Contains(searchText) ?? false) ||
                (t.Album?.ToLower().Contains(searchText) ?? false)
            ).ToList();

            if (results.Any())
            {
                // Есть результаты - показываем список, скрываем пустое состояние
                SearchResultsList.ItemsSource = results;
                SearchResultsList.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Нет результатов - показываем пустое состояние, скрываем список
                SearchResultsList.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;

                // Можем изменить текст для "ничего не найдено"
                var emptyText = (EmptyStatePanel.Children[1] as TextBlock);
                if (emptyText != null)
                {
                    emptyText.Text = "Ничего не найдено";
                }
            }

            System.Diagnostics.Debug.WriteLine($"Поиск: найдено {results.Count} результатов");
        }

        /// <summary>
        /// Обработчик клика по результату поиска
        /// </summary>
        private async void SearchResult_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(track.FilePath);
                    PlayTrack(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при воспроизведении: {ex.Message}");

                    // Удаляем из коллекции если файл не найден
                    _tracks.Remove(track);

                    // Обновляем результаты поиска
                    SearchBox_TextChanged(null, null);
                }
            }
        }
    }
}