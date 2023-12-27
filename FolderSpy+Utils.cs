﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using Notification.Wpf;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Notification.Wpf.Classes;

namespace AutoTotal {
    internal static class FolderSpy {
        private static readonly Dictionary<string, FileSystemWatcher> watchers = new();
        private static readonly string[] extensions = new string[61] {
            ".apk", ".app", ".exe", ".ee", ".bat", ".cmd", ".ps1", ".cpl", ".cab", ".com",
            ".gadget", ".scr", ".lnk", ".msi", ".mst", ".msp", ".pif", ".paf", ".reg", ".rgs",
            ".vb", ".vbe", ".vbs", ".vbscript", ".ws", ".wsf", ".wsh", ".ahk", ".pdf", ".chm",
            ".docm", ".dotm", ".jar", ".js", ".jse", ".sct", ".jsx", ".mam", ".otm", ".potm",
            ".ppam", ".ppsm", ".pptm", ".py", ".pyc", ".pyo", ".xlam", ".xlsm", ".xltm", ".hta",
            ".dll", ".sys", ".drv", ".zip", ".rar", ".7z", ".iso", ".img", ".tar", ".wim",
            ".xz"};

        public static void Add(string? path) {
            if (path == null) return;
            if (watchers.ContainsKey(path)) return;
            var watcher = new FileSystemWatcher(path);
            watcher.Created += (sender, e) => Task.Run(() => OnFileCreated(sender, e));
            watcher.Renamed += (sender, e) => Task.Run(() => OnFileCreated(sender, e));
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;
            watchers[path] = watcher;
        }

        public static void Remove(string? path) {
            if (path == null) return;
            if (watchers.TryGetValue(path, out FileSystemWatcher? watcher)) {
                watcher.Dispose();
                watchers.Remove(path);
            };
        }

        private static async Task OnFileCreated(object sender, FileSystemEventArgs e) {
            if (extensions.Contains(Path.GetExtension(e.FullPath), StringComparer.OrdinalIgnoreCase)) {
                if (Properties.Settings.Default.BlockFiles) Blocker.Block(e.FullPath);
                await Utils.ScanFile(e.FullPath);
                if (Properties.Settings.Default.BlockFiles) Blocker.Unblock(e.FullPath);
            }
        }
    }

    internal static class Utils {
        public static BitmapImage ToBitmapImage(this Bitmap bitmap) {
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

        public static async Task ScanFile(string path, bool ContinueRun = true) {
            TaskCompletionSource<bool> notificationWaiter = new();

            if (!File.Exists(path)) {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Data.notificationManager.Show(new NotificationContent {
                        Title = "AutoTotal",
                        Message = "Указанный файл не существует",
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
                Data.notificationManager.Show(new NotificationContent {
                    Title = "AutoTotal",
                    Message = "Начато сканирование " + Path.GetFileName(path),
                    Type = NotificationType.Notification,
                    TrimType = NotificationTextTrimType.NoTrim,
                    Icon = Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage(),
                }, expirationTime: TimeSpan.FromSeconds(5));
            });
            string md5 = BitConverter.ToString(MD5.Create().ComputeHash(File.OpenRead(path))).Replace("-", "").ToLower();
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("x-apikey", Properties.Settings.Default.VTKey);
            
            HttpResponseMessage response;
            try {
                response = await httpClient.GetAsync("https://www.virustotal.com/api/v3/files/" + md5);
            }
            catch (HttpRequestException) {
                MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! Проверьте подключение к интернету!", "AutoTotal — Ошибка сканирования", MessageBoxButton.OK, MessageBoxImage.Error);
                if (!ContinueRun) Environment.Exit(1);
                return;
            }
            JObject result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
            int detects;

            if (result["error"]?["code"]?.ToString() == "NotFoundError") {
                // Файла нет на VT, нужно загружать
                long size = new FileInfo(path).Length;
                if (size / 1048576 >= 650) {
                    MessageBox.Show(Path.GetFileName(path) + "весит больше 650 МБ, его нельзя просканировать на VirusTotal! Будьте осторожны, убедитесь, что источнику можно доверять!", "AutoTotal - Файл не просканирован", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (!ContinueRun) Environment.Exit(1);
                    return;
                }
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    Data.progressbars.Add(path, Data.notificationManager.ShowProgressBar("Отправка " + Path.GetFileName(path) + " на VirusTotal", false, false, "", true, 1U, "", icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage()));
                });
                try {
                    string uploadUrl = size / 1048576 < 32 ?
                        "https://www.virustotal.com/api/v3/files" :
                        JsonConvert.DeserializeObject<JObject>(await httpClient.GetStringAsync("https://www.virustotal.com/api/v3/files/upload_url"))!["data"]!.ToString();

                    using MultipartFormDataContent multi = new();
                    FileStream fs = new(path, FileMode.Open, FileAccess.Read);
                    StreamContent fileContent = new(fs);
                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") {
                        Name = "\"file\"",
                        FileName = "\"" + Path.GetFileName(path) + "\""
                    };
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    multi.Add(fileContent);

                    // Запускаем в фоне метод, который будет получать и устанавливать прогресс
                    new Task(new Action(() => ProgressTracker(Path.GetFileName(path), fs, Data.progressbars[path]))).Start();
                    try {
                        response = await httpClient.PostAsync(uploadUrl, multi, Data.progressbars[path].Cancel);
                    }
                    catch (HttpRequestException) {
                        MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! Проверьте подключение к интернету!", "AutoTotal — Ошибка сканирования", MessageBoxButton.OK, MessageBoxImage.Error);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            Data.progressbars[path].Dispose();
                            Data.progressbars.Remove(path);
                        });
                        if (!ContinueRun) Environment.Exit(1);
                        return;
                    }
                    catch (OperationCanceledException) {
                        System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            Data.progressbars[path].Dispose();
                        });
                        if (!ContinueRun) Environment.Exit(1);
                        return;
                    }

                    string resp = await response.Content.ReadAsStringAsync();
                    string url = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!["data"]!["links"]!["self"]!.ToString();
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars[path].Dispose();
                        Data.progressbars[path] = Data.notificationManager.ShowProgressBar("VirusTotal сканирует " + Path.GetFileName(path), false, false, "", true, 1U, "", icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage());
                    });

                    // Ждём, когда сканирование завершится
                    while (true) {
                        response = await httpClient.GetAsync(url);
                        result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
                        if (result["error"]?["code"]?.ToString() == "QuotaExceededError") {
                            MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! К сожалению, достигнут лимит использования API VirusTotal. Попробуйте просканировать снова через некоторое время. Либо создайте новый аккаунт VirusTotal и измените ключ на новый.", "AutoTotal — Лимит API VirusTotal", MessageBoxButton.OK, MessageBoxImage.Error);
                            if (!ContinueRun) Environment.Exit(1);
                            return;
                        }
                        if (result["data"]!["attributes"]!["status"]!.ToString() == "completed") break;
                        await Task.Delay(3000);
                    }
                }
                catch (HttpRequestException) {
                    MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! Проверьте подключение к интернету!", "AutoTotal — Ошибка сканирования", MessageBoxButton.OK, MessageBoxImage.Error);
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
            else if (result["error"]?["code"]?.ToString() == "QuotaExceededError") {
                MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! К сожалению, достигнут лимит использования API VirusTotal. Попробуйте просканировать снова через некоторое время. Либо создайте новый аккаунт VirusTotal и измените ключ на новый.", "AutoTotal — Лимит API VirusTotal", MessageBoxButton.OK, MessageBoxImage.Error);
                if (!ContinueRun) Environment.Exit(1);
                return;
            }
            else {
                // Если файл в данный момент сканируется, ожидаем завершения
                if (result["data"]!["attributes"]!["last_analysis_date"] == null) {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.progressbars.Add(path, Data.notificationManager.ShowProgressBar("VirusTotal сканирует " + Path.GetFileName(path), false, false, "", true, 1U, "Отправка файла", icon: Icon.ExtractAssociatedIcon(path)?.ToBitmap().ToBitmapImage()));
                    });
                    try {
                        while (true) {
                            response = await httpClient.GetAsync(result["data"]!["links"]!["self"]!.ToString());
                            result = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync())!;
                            if (result["error"]?["code"]?.ToString() == "QuotaExceededError") {
                                MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! К сожалению, достигнут лимит использования API VirusTotal. Попробуйте просканировать снова через некоторое время. Либо создайте новый аккаунт VirusTotal и измените ключ на новый.", "AutoTotal — Лимит API VirusTotal", MessageBoxButton.OK, MessageBoxImage.Error);
                                if (!ContinueRun) Environment.Exit(1);
                                return;
                            }
                            if (result["data"]!["attributes"]!["last_analysis_date"] != null) break;
                            await Task.Delay(3000);
                        }
                    }
                    catch (HttpRequestException) {
                        MessageBox.Show("Файл " + Path.GetFileName(path) + " не просканирован! Проверьте подключение к интернету!", "AutoTotal — Ошибка сканирования", MessageBoxButton.OK, MessageBoxImage.Error);
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
                NotificationContent content = new() {
                    TrimType = NotificationTextTrimType.NoTrim,
                    LeftButtonAction = () => Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c start \"\" \"{path}\"", UseShellExecute = true }),
                    LeftButtonContent = "Запустить",
                    Message = detects + " антивирусов пометили файл опасным"
                };
                if (detects == 0) {
                    content.Type = NotificationType.Notification;
                    content.Title = Path.GetFileName(path) + " чист!";
                    content.Message = "Ни один антивирус не пометил его опасным";
                    content.Icon = new BitmapImage(new Uri("pack://application:,,,/res/like.png"));
                }
                else if (detects < 5) {
                    content.Type = NotificationType.Warning;
                    content.Title = Path.GetFileName(path) + " подозрителен!";
                    content.LeftButtonAction = () => Process.Start("explorer", "https://virustotal.com/gui/file/" + md5);
                    content.LeftButtonContent = "Показать отчёт";
                    content.RightButtonContent = "Удалить файл";
                    content.RightButtonAction = () => { File.Delete(path); };
                }
                else {
                    content.Type = NotificationType.Error;
                    content.Title = Path.GetFileName(path) + " опасен!";
                    content.LeftButtonAction = () => Process.Start("explorer", "https://virustotal.com/gui/file/" + md5);
                    content.LeftButtonContent = "Показать отчёт";
                    content.RightButtonContent = "Удалить файл";
                    content.RightButtonAction = () => { File.Delete(path); };
                }
                Data.notificationManager.Show(content, expirationTime: TimeSpan.FromSeconds(10), onClose: () => notificationWaiter.TrySetResult(true));
            });
            await notificationWaiter.Task;
            if (!ContinueRun) Environment.Exit(1);
        }

        static void ProgressTracker(string name, FileStream fs, NotifierProgress<(double?, string, string, bool?)> progressbar) {
            int pos = 0;
            while (!fs.CanRead || pos != 100) {
                pos = (int)Math.Round(100 * (fs.Position / (double)fs.Length));
                System.Windows.Application.Current.Dispatcher.Invoke(() => progressbar.Report((pos, "", "Отправка " + name + " на VirusTotal", true)));
                Thread.Sleep(100);
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() => progressbar.Report((0, "", "VirusTotal обрабатывает " + name, false)));
        }
    }
}