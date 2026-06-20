using System.IO;
using Notification.Wpf;

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
        private static readonly string[] confirm_extensions = new string[9] {
            ".bat", ".cmd", ".js", ".pdf", ".ps1", ".py", ".pyc", ".pyo", ".vbs"
        };

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
                if (new FileInfo(e.FullPath).Length == 0) return;

                async Task ScanTask() {
                    if (Properties.Settings.Default.BlockFiles) Blocker.Block(e.FullPath);
                    await Utils.ScanFile(e.FullPath);
                    if (Properties.Settings.Default.BlockFiles) Blocker.Unblock(e.FullPath);
                }

                if (e.Name!.Contains("AyuGram Desktop\\") || e.Name.Contains("Telegram Desktop\\")) {
                    TaskCompletionSource<bool> notificationWaiter = new();
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.notificationManager.Show(new NotificationContent {
                            Title = Properties.Resources.DidFileDownload.Replace("%name%", Path.GetFileName(e.Name)),
                            Message = Properties.Resources.CantTrackTelegram,
                            Type = NotificationType.Notification,
                            TrimType = NotificationTextTrimType.NoTrim,
                            Icon = Icon.ExtractAssociatedIcon(e.FullPath)?.ToBitmap().ToBitmapImage(),
                            LeftButtonContent = Properties.Resources.Downloaded,
                            LeftButtonAction = () => notificationWaiter.TrySetResult(true),
                            RightButtonContent = Properties.Resources.DontScan,
                            RightButtonAction = () => notificationWaiter.TrySetResult(false)
                        }, expirationTime: Timeout.InfiniteTimeSpan, onClose: () => notificationWaiter.TrySetResult(true));
                    });
                    await notificationWaiter.Task;
                    if (notificationWaiter.Task.Result) await ScanTask();
                    return;
                }

                if (confirm_extensions.Contains(Path.GetExtension(e.FullPath), StringComparer.OrdinalIgnoreCase)) {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        Data.notificationManager.Show(new NotificationContent {
                            Title = $"{Properties.Resources.Scan} {Path.GetFileName(e.Name)}?",
                            Message = Properties.Resources.UploadedOnlyUponConfirmation,
                            Type = NotificationType.None,
                            TrimType = NotificationTextTrimType.NoTrim,
                            Icon = Icon.ExtractAssociatedIcon(e.FullPath)?.ToBitmap().ToBitmapImage(),
                            LeftButtonAction = async () => await ScanTask(),
                            LeftButtonContent = Properties.Resources.Scan,
                            RightButtonContent = Properties.Resources.No,
                            RightButtonAction = () => {},
                        }, expirationTime: TimeSpan.FromSeconds(300));
                    });
                    return;
                }
                await ScanTask();
            }
        }
    }
}