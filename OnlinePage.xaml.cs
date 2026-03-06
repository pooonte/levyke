using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;

namespace levyke.Views
{
    public sealed partial class OnlinePage : Page
    {
        private HttpClient _client = new HttpClient();
        private ObservableCollection<RemoteTrack> _tracks = new ObservableCollection<RemoteTrack>();
        private string _serverUrl = "http://192.168.3.35:8080";
        private RemoteTrack _currentTrack;
        private bool _isPlaying = false;

        public class RemoteTrack
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Folder { get; set; }
        }

        public OnlinePage()
        {
            this.InitializeComponent();
            LoadTracks();
        }

        private async void LoadTracks()
        {
            try
            {
                var json = await _client.GetStringAsync($"{_serverUrl}/");
                var tracks = JsonConvert.DeserializeObject<RemoteTrack[]>(json);
                _tracks.Clear();
                foreach (var t in tracks) _tracks.Add(t);
                OnlineTracksList.ItemsSource = _tracks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void OnlineTrack_ItemClick(object sender, ItemClickEventArgs e)
        {
            var track = e.ClickedItem as RemoteTrack;
            if (track == null) return;

            _currentTrack = track;
            var url = $"{_serverUrl}/stream/{Uri.EscapeDataString(track.Path)}";
            var source = MediaSource.CreateFromUri(new Uri(url));

            var player = Windows.Media.Playback.BackgroundMediaPlayer.Current;
            player.Source = source;
            player.Play();

            _isPlaying = true;
            NowPlayingTitle.Text = track.Name;
            NowPlayingArtist.Text = track.Folder;
            OnlinePlayer.Visibility = Visibility.Visible;
            UpdateButton();
        }

        private void OnlinePlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            var player = Windows.Media.Playback.BackgroundMediaPlayer.Current;
            if (_isPlaying)
            {
                player.Pause();
                _isPlaying = false;
            }
            else
            {
                player.Play();
                _isPlaying = true;
            }
            UpdateButton();
        }

        private void UpdateButton()
        {
            string icon = _isPlaying ? "pause.png" : "play.png";
            OnlinePlayPauseIcon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/{icon}"));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }
}