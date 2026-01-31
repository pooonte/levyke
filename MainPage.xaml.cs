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
        private List<ColorPalette> _themes;
        private bool _userIsSeeking = false;
        private string _currentAlbumName;
        private ObservableCollection<TrackItem> _currentAlbumTracks = new ObservableCollection<TrackItem>();
        private int _currentTrackIndex = -1;

        private StreamSocket _socket;
        private DataWriter _writer;
        private DataReader _reader;


        public MainPage()
        {
            this.InitializeComponent();
            MediaPlayerSingleton.Player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            LoadMusicFiles();
            LoadThemes();
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


                _tracks.Clear();

                foreach (var file in files)
                {
                    var track = await TrackItem.FromFile(file);
                    _tracks.Add(track);
                }

                TracksList.ItemsSource = _tracks;

                var artists = _tracks
                    .Select(t => t.Artist)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();
                ArtistsList.ItemsSource = artists;

                var albums = _tracks
                    .Where(t => t.Album != "Неизвестный альбом")
                    .Select(t => t.Album)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();
                AlbumsList.ItemsSource = albums;
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

            var track = _tracks.FirstOrDefault(t => t.File == file);
            if (track == null) return;

            _currentTrackIndex = _tracks.IndexOf(track);

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

        private void Track_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TrackItem track)
            {
                PlayTrack(track.File);
            }
        }

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
            PlayTrack(_tracks[_currentTrackIndex].File);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tracks.Count == 0 || _currentTrackIndex < 0) return;
            _currentTrackIndex = Math.Min(_tracks.Count - 1, _currentTrackIndex + 1);
            PlayTrack(_tracks[_currentTrackIndex].File);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            // реализовать кнопку повтора
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

                this.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MiniPlayerPanel.Background = (Brush)Application.Current.Resources["MiniPlayerBackgroundBrush"];
                MiniPlayButton.Background = (Brush)Application.Current.Resources["PlaybackControlBrush"];
                ThemeSelector.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                MainName.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                FullPlayerOverlay.Background = (Brush)Application.Current.Resources["MainBackgroundBrush"];
                ProgressSlider.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
                VolumeSlider.Foreground = (Brush)Application.Current.Resources["AppTitleBrush"];
            }
        }

        private void GroupTracks()
        {
            var artistGroups = _tracks
                .GroupBy(t => t.Artist)
                .Select(g => new ArtistItem
                {
                    Name = g.Key,
                    FirstTrack = g.First()
                })
                .OrderBy(a => a.Name)
                .ToList();

            _artists.Clear();
            foreach (var artist in artistGroups)
                _artists.Add(artist);

            var albumGroups = _tracks
                .GroupBy(t => t.Album)
                .Where(g => g.Key != "Неизвестный альбом")
                .Select(g => new AlbumItem
                {
                    Name = g.Key,
                    FirstTrack = g.First()
                })
                .OrderBy(a => a.Name)
                .ToList();

            _albums.Clear();
            foreach (var album in albumGroups)
                _albums.Add(album);
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
    }
}