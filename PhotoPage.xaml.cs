using System;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace levyke
{
    public sealed partial class PhotoPage : Page
    {
        public PhotoPage()
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
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    FullPhoto.Source = bitmap;
                }
                catch (Exception ex)
                {
                    var dialog = new Windows.UI.Popups.MessageDialog("Не удалось загрузить фото: " + ex.Message);
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
