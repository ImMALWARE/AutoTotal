using Microsoft.Win32;
using Notification.Wpf;
using Notification.Wpf.Classes;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace AutoTotal {

    public static class Data {
        internal static readonly NotificationManager notificationManager = new();
        internal static readonly Dictionary<string, NotifierProgress<(double?, string, string, bool?)>> progressbars = new();
        internal static Mutex? mutex;
        internal static SettingsWindow? settings;
    }
    
    public partial class App : Application {
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            Data.mutex = new Mutex(true, "AutoTotalMutex", out bool isOnlyInstance);
            if (string.IsNullOrEmpty(AutoTotal.Properties.Settings.Default.VTKey)) {
                // Первый запуск
                if (!isOnlyInstance) {
                    System.Windows.MessageBox.Show(AutoTotal.Properties.Resources.FinishFirstSetup, "AutoTotal", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                }
                new SetKeyWindow().ShowDialog();
                AutoTotal.Properties.Settings.Default.Folders = new() { Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", string.Empty)?.ToString() };
                AutoTotal.Properties.Settings.Default.Save();
                if (!Autorun.Exists()) Autorun.Add();
                Data.notificationManager.Show(new NotificationContent {
                    Title = "AutoTotal",
                    Message = AutoTotal.Properties.Resources.ProgramWillAutostart,
                    Type = NotificationType.Information,
                    TrimType = NotificationTextTrimType.NoTrim,
                    LeftButtonAction = () => ShowSettings(),
                    LeftButtonContent = AutoTotal.Properties.Resources.Settings
                }, expirationTime: TimeSpan.FromSeconds(10));
            }

            new Window {
                Width = 1,
                Height = 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden
            }.Show();

            // Сканирование из командной строки
            if (e.Args.Length == 2 && e.Args[0] == "/scan") Task.Run(async () => await Utils.ScanFile(e.Args[1], isOnlyInstance));
            if (isOnlyInstance) {
                NotifyIcon notifyIcon = new() {
                    Visible = true,
                    Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory + "at.ico"),
                    Text = AutoTotal.Properties.Resources.WorkingInBackground,
                    ContextMenuStrip = new ContextMenuStrip {
                        Items = {
                            new ToolStripMenuItem(AutoTotal.Properties.Resources.Settings, null, (s, e) => ShowSettings()),
                            new ToolStripMenuItem(AutoTotal.Properties.Resources.AddFolder, null, (s, e) => {
                                if (Data.settings?.IsVisible ?? false) {
                                    System.Windows.MessageBox.Show(AutoTotal.Properties.Resources.AddFolderForce, AutoTotal.Properties.Resources.AddingFolder, MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }
                                using var folderBrowserDialog = new FolderBrowserDialog();
                                folderBrowserDialog.SelectedPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", string.Empty)!.ToString()!;
                                DialogResult result = folderBrowserDialog.ShowDialog();
                                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath)) {
                                    if (!AutoTotal.Properties.Settings.Default.Folders.Contains(folderBrowserDialog.SelectedPath)) {
                                        AutoTotal.Properties.Settings.Default.Folders.Add(folderBrowserDialog.SelectedPath);
                                        AutoTotal.Properties.Settings.Default.Save();
                                        FolderSpy.Add(folderBrowserDialog.SelectedPath);
                                    }
                                }
                            }),
                            new ToolStripMenuItem(AutoTotal.Properties.Resources.ScanFile, null, (s, e) => {
                                using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
                                DialogResult result = openFileDialog.ShowDialog();
                                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName)) Task.Run(async () => await Utils.ScanFile(openFileDialog.FileName));
                            }),
                            new ToolStripMenuItem(AutoTotal.Properties.Resources.ChangeVTKey, null, (s, e) => new SetKeyWindow().Show()),
                            new ToolStripMenuItem(AutoTotal.Properties.Resources.Exit, null, (s, e) => Environment.Exit(1))
                        }
                    }
                };
                if (!e.Args.Contains("/autorun")) {
                    Data.notificationManager.Show(new NotificationContent {
                        Title = "AutoTotal",
                        Message = AutoTotal.Properties.Resources.Started,
                        Type = NotificationType.Notification,
                        TrimType = NotificationTextTrimType.NoTrim,
                        LeftButtonAction = () => ShowSettings(),
                        LeftButtonContent = AutoTotal.Properties.Resources.Settings,
                        Icon = new BitmapImage(new Uri("pack://application:,,,/res/virustotal.png")),
                    }, expirationTime: TimeSpan.FromSeconds(5));
                }
                StringCollection ToRemove = new();
                foreach (string? path in AutoTotal.Properties.Settings.Default.Folders) {
                    if (!Directory.Exists(path)) {
                        ToRemove.Add(path);
                        Data.notificationManager.Show(new NotificationContent {
                            Title = AutoTotal.Properties.Resources.DisappearedFolder,
                            Message = path,
                            Type = NotificationType.Information,
                            TrimType = NotificationTextTrimType.NoTrim,
                            LeftButtonAction = () => ShowSettings(),
                            LeftButtonContent = AutoTotal.Properties.Resources.Settings
                        }, expirationTime: TimeSpan.FromSeconds(10));
                    }
                    else FolderSpy.Add(path);
                };
                if (ToRemove.Count > 0) {
                    foreach (string? path in ToRemove) AutoTotal.Properties.Settings.Default.Folders.Remove(path);
                    AutoTotal.Properties.Settings.Default.Save();
                }
            }
        }

        private static void ShowSettings() {
            if (Data.settings == null) {
                Data.settings = new SettingsWindow();
                Data.settings.Closed += (sender, e) => Data.settings = null;
                Data.settings.Show();
            }
            else Data.settings.Focus();
        }
    }
}
