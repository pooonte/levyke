using System;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace levyke
{
    public sealed partial class VideoPage : Page
    {
        public VideoPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is StorageFile file)
            {
                try
                {
                    var stream = await file.OpenReadAsync();
                    VideoPlayer.SetSource(stream, file.ContentType);
                    VideoPlayer.Play();
                }
                catch (Exception ex)
                {
                    var dialog = new Windows.UI.Popups.MessageDialog("Не удалось воспроизвести видео: " + ex.Message);
                    await dialog.ShowAsync();
                }
            }
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
