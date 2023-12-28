using Notification.Wpf;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace AutoTotal {
    public partial class SetKeyWindow : Window {
        public SetKeyWindow() {
            InitializeComponent();
        }

        private async void Save(object sender, RoutedEventArgs e) {
            SaveButton.IsEnabled = false;
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("x-apikey", KeyBox.Password);
            HttpResponseMessage response;
            try {
                response = await httpClient.GetAsync("https://www.virustotal.com/api/v3/domains/google.com");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex) {
                MessageBox.Show(ex.Message.Contains("Unauthorized") ? Properties.Resources.InvalidKey : Properties.Resources.CheckKeyError + ex.Message, Properties.Resources.CheckingKey, MessageBoxButton.OK, MessageBoxImage.Error);
                SaveButton.IsEnabled = true;
                return;
            }

            Properties.Settings.Default.VTKey = KeyBox.Password;
            Properties.Settings.Default.Save();
            Data.notificationManager.Show(new NotificationContent {
                Title = "AutoTotal",
                Message = Properties.Resources.KeyChanged,
                Type = NotificationType.Notification,
                TrimType = NotificationTextTrimType.NoTrim,
                Icon = new BitmapImage(new Uri("pack://application:,,,/res/virustotal.png")),
            }, expirationTime: TimeSpan.FromSeconds(5));
            Close();
        }

        private void Help(object sender, RoutedEventArgs e) {
            Process.Start("explorer", Properties.Resources.WikiLink);
        }
    }
}