using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Notification.Wpf;
using Notification.Wpf.Classes;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;

namespace AutoTotal
{
    internal static class Utils
    {
        public static BitmapImage ToBitmapImage(this Bitmap bitmap)
        {
            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        public static async Task ScanFile(string path, bool ContinueRun = true)
        {
            TaskCompletionSource<bool> notificationWaiter = new();

            if (Data.progressbars.ContainsKey(path)) return;

            if (!File.Exists(path))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Data.notificationManager.Show(new NotificationContent
                    {
                        Title = "AutoTotal",
                        Message = Properties.Resources.FileDoesntExist,
                        Type = NotificationType.Error,
                        TrimType = NotificationTextTrimType.NoTrim,
                        Icon = new BitmapImage(new Uri("pack://application:,,,/res/error.png"))
                    }, expirationTime: TimeSpan.FromSeconds(5), onClose: () => notificationWaiter.TrySetResult(true));
                });
                await notificationWaiter.Task;
                if (!ContinueRun) Environment.Exit(1);
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                Data.notificationManager.Show(new NotificationContent
                {
                    Title = "AutoTotal",
                    Message = Properties.Resources.Scanning + Path.GetFileName(path),
                    Type = NotificationType.Notification,
                    TrimType = NotificationTextTrimType.NoTrim,
                    Icon = Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage(),
                }, expirationTime: TimeSpan.FromSeconds(5));
            });
            string md5 = BitConverter.ToString(MD5.Create().ComputeHash(File.OpenRead(path))).Replace("-", "").ToLower();
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("x-apikey", Properties.Settings.Default.VTKey);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync("https://www.virustotal.com/api/v3/files/" + md5);
            }
            catch (HttpRequestException)
            {
                MessageBox.Show(Properties.Resources.CheckInternetConnection.Replace("%name%", Path.GetFileName(path)), Properties.Resources.ScanError, MessageBoxButton.OK, MessageBoxImage.Error);
                if (!ContinueRun) Environment.Exit(1);
                return;
            }
            JObject result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
            int detects;

            if (result["error"]?["code"]?.ToString() == "NotFoundError")
            {
                // Файла нет на VT, нужно загружать
                long size = new FileInfo(path).Length;
                if (size / 1048576 >= 650)
                {
                    MessageBox.Show(Path.GetFileName(path) + Properties.Resources.FileTooBig, Properties.Resources.ScanError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (!ContinueRun) Environment.Exit(1);
                    return;
                }
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Data.progressbars.Add(path, Data.notificationManager.ShowProgressBar(Properties.Resources.Uploading.Replace("%name%", Path.GetFileName(path)), false, false, "", true, 1U, "...", icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage()));
                });
                try
                {
                    string uploadUrl = size / 1048576 < 32 ?
                        "https://www.virustotal.com/api/v3/files" :
                        JsonConvert.DeserializeObject<JObject>(await httpClient.GetStringAsync("https://www.virustotal.com/api/v3/files/upload_url"))!["data"]!.ToString();

                    using MultipartFormDataContent multi = new();
                    FileStream fs = new(path, FileMode.Open, FileAccess.Read);
                    StreamContent fileContent = new(fs);
                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "\"file\"",
                        FileName = "\"" + Path.GetFileName(path) + "\""
                    };
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    multi.Add(fileContent);

                    // Запускаем в фоне метод, который будет получать и устанавливать прогресс
                    new Task(new Action(() => ProgressTracker(Path.GetFileName(path), fs, Data.progressbars[path]))).Start();
                    try
                    {
                        response = await httpClient.PostAsync(uploadUrl, multi, Data.progressbars[path].Cancel);
                    }
                    catch (HttpRequestException)
                    {
                        MessageBox.Show(Properties.Resources.CheckInternetConnection.Replace("%name%", Path.GetFileName(path)), Properties.Resources.ScanError, MessageBoxButton.OK, MessageBoxImage.Error);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            Data.progressbars[path].Dispose();
                            Data.progressbars.Remove(path);
                        });
                        if (!ContinueRun) Environment.Exit(1);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            Data.progressbars[path].Dispose();
                            Data.progressbars.Remove(path);
                        });
                        if (!ContinueRun) Environment.Exit(1);
                        return;
                    }

                    string resp = await response.Content.ReadAsStringAsync();
                    string url = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!["data"]!["links"]!["self"]!.ToString();
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars[path].Dispose();
                        Data.progressbars[path] = Data.notificationManager.ShowProgressBar(Properties.Resources.IsScanning + Path.GetFileName(path), false, false, "", true, 1U, "", icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage());
                    });

                    // Ждём, когда сканирование завершится
                    while (true)
                    {
                        response = await httpClient.GetAsync(url);
                        result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
                        if (result["error"]?["code"]?.ToString() == "QuotaExceededError")
                        {
                            MessageBox.Show(Properties.Resources.QuotaExceeded.Replace("%name%", Path.GetFileName(path)), Properties.Resources.TitleLimit, MessageBoxButton.OK, MessageBoxImage.Error);
                            if (!ContinueRun) Environment.Exit(1);
                            return;
                        }
                        if (result["data"]!["attributes"]!["status"]!.ToString() == "completed") break;
                        await Task.Delay(3000);
                    }
                }
                catch (HttpRequestException)
                {
                    MessageBox.Show(Properties.Resources.CheckInternetConnection.Replace("%name%", Path.GetFileName(path)), Properties.Resources.ScanError, MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars[path].Dispose();
                        Data.progressbars.Remove(path);
                    });
                    if (!ContinueRun) Environment.Exit(1);
                    return;
                }
                detects = Convert.ToInt32(result["data"]!["attributes"]!["stats"]!["malicious"]);
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Data.progressbars[path].Dispose();
                    Data.progressbars.Remove(path);
                });
            }
            else if (result["error"]?["code"]?.ToString() == "QuotaExceededError")
            {
                MessageBox.Show(Properties.Resources.QuotaExceeded.Replace("%name%", Path.GetFileName(path)), Properties.Resources.TitleLimit, MessageBoxButton.OK, MessageBoxImage.Error);
                if (!ContinueRun) Environment.Exit(1);
                return;
            }
            else
            {
                // Если файл в данный момент сканируется, ожидаем завершения
                if (result["data"]!["attributes"]!["last_analysis_date"] == null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars.Add(path, Data.notificationManager.ShowProgressBar(Properties.Resources.IsScanning + Path.GetFileName(path), false, false, "", true, 1U, Properties.Resources.SendingFile, icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage()));
                    });
                    try
                    {
                        while (true)
                        {
                            response = await httpClient.GetAsync(result["data"]!["links"]!["self"]!.ToString());
                            result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
                            if (result["error"]?["code"]?.ToString() == "QuotaExceededError")
                            {
                                MessageBox.Show(Properties.Resources.QuotaExceeded.Replace("%name%", Path.GetFileName(path)), Properties.Resources.TitleLimit, MessageBoxButton.OK, MessageBoxImage.Error);
                                if (!ContinueRun) Environment.Exit(1);
                                return;
                            }
                            if (result["data"]!["attributes"]!["last_analysis_date"] != null) break;
                            await Task.Delay(3000);
                        }
                    }
                    catch (HttpRequestException)
                    {
                        MessageBox.Show(Properties.Resources.CheckInternetConnection.Replace("%name%", Path.GetFileName(path)), Properties.Resources.ScanError, MessageBoxButton.OK, MessageBoxImage.Error);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            Data.progressbars[path].Dispose();
                            Data.progressbars.Remove(path);
                        });
                        if (!ContinueRun) Environment.Exit(1);
                        return;
                    }
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars[path].Dispose();
                        Data.progressbars.Remove(path);
                    });
                }
                detects = Convert.ToInt32(result["data"]!["attributes"]!["last_analysis_stats"]!["malicious"]);
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                long timestamp = (long?)result["data"]?["attributes"]?["last_analysis_date"] ?? 
                                 (long?)result["data"]?["attributes"]?["date"] ?? 
                                 DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string dateStr = $"\n{Properties.Resources.LastScanDate}{DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime:g}";

                NotificationContent content = new()
                {
                    TrimType = NotificationTextTrimType.NoTrim,
                    LeftButtonAction = () => Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c start \"\" \"{path}\"", UseShellExecute = true }),
                    LeftButtonContent = Properties.Resources.Run,
                    Message = detects + Properties.Resources.AVsDetected + dateStr
                };

                if (detects == 0)
                {
                    content.Type = NotificationType.Information;
                    content.Title = Path.GetFileName(path) + Properties.Resources.Clean;
                    content.Message = Properties.Resources._0detects + dateStr;
                    content.Icon = new BitmapImage(new Uri("pack://application:,,,/res/like.png"));
                    content.RightButtonAction = () => Process.Start("explorer", "https://virustotal.com/gui/file/" + md5);
                    content.RightButtonContent = Properties.Resources.ShowReport;
                }
                else
                {
                    content.Type = detects < 5 ? NotificationType.Warning : NotificationType.Error;
                    content.Title = Path.GetFileName(path) + (detects < 5 ? Properties.Resources.Suspicious : Properties.Resources.Dangerous);
                    content.LeftButtonAction = () => Process.Start("explorer", "https://virustotal.com/gui/file/" + md5);
                    content.LeftButtonContent = Properties.Resources.ShowReport;
                    content.RightButtonAction = () => { File.Delete(path); };
                    content.RightButtonContent = Properties.Resources.Delete;
                }

                Data.notificationManager.Show(content, expirationTime: TimeSpan.FromSeconds(10), onClose: () => notificationWaiter.TrySetResult(true));
            });
            await notificationWaiter.Task;
            if (!ContinueRun) Environment.Exit(1);
        }

        static void ProgressTracker(string name, FileStream fs, NotifierProgress<(double?, string, string, bool?)> progressbar)
        {
            int pos = 0;
            try
            {
                while (!fs.CanRead || pos != 100)
                {
                    pos = (int)Math.Round(100 * (fs.Position / (double)fs.Length));
                    System.Windows.Application.Current.Dispatcher.Invoke(() => progressbar.Report((pos, "", Properties.Resources.Uploading.Replace("%name%", name), true)));
                    Thread.Sleep(100);
                }
                System.Windows.Application.Current.Dispatcher.Invoke(() => progressbar.Report((0, "", Properties.Resources.Processing + name, false)));
            }
            catch (ObjectDisposedException)
            {
                // Поток был закрыт после нажатия кнопки Отмена
            }
        }
    }
}
