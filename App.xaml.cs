﻿using Microsoft.Win32;
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
                    System.Windows.MessageBox.Show("Сначала завершите первую настройку!", "AutoTotal", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                }
                new SetKeyWindow().ShowDialog();
                AutoTotal.Properties.Settings.Default.Folders = new() { Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", string.Empty)?.ToString() };
                AutoTotal.Properties.Settings.Default.Save();
                if (!Autorun.Exists()) Autorun.Add();
                Data.notificationManager.Show(new NotificationContent {
                    Title = "AutoTotal",
                    Message = "Программа будет запускаться автоматически при старте Windows! Это можно изменить в настройках",
                    Type = NotificationType.Information,
                    TrimType = NotificationTextTrimType.NoTrim,
                    LeftButtonAction = () => ShowSettings(),
                    LeftButtonContent = "Настройки"
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
                    Icon = new Icon(AppDomain.CurrentDomain.BaseDirectory+"\\res\\at.ico"),
                    Text = "AutoTotal работает в фоне",
                    ContextMenuStrip = new ContextMenuStrip {
                        Items = {
                            new ToolStripMenuItem("Настройки", null, (s, e) => ShowSettings()),
                            new ToolStripMenuItem("Добавить папку", null, (s, e) => {
                                if (Data.settings?.IsVisible ?? false) {
                                    System.Windows.MessageBox.Show("Добавьте папку в окне настроек!", "Добавление папки", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            new ToolStripMenuItem("Просканировать файл...", null, (s, e) => {
                                using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
                                DialogResult result = openFileDialog.ShowDialog();
                                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName)) Task.Run(async () => await Utils.ScanFile(openFileDialog.FileName));
                            }),
                            new ToolStripMenuItem("Изменить ключ API VirusTotal", null, (s, e) => new SetKeyWindow().Show()),
                            new ToolStripMenuItem("Выход", null, (s, e) => Environment.Exit(1))
                        }
                    }
                };
                if (!e.Args.Contains("/autorun")) {
                    Data.notificationManager.Show(new NotificationContent {
                        Title = "AutoTotal",
                        Message = "Программа была запущена в фоне, используйте значок в трее для взаимодействия",
                        Type = NotificationType.Notification,
                        TrimType = NotificationTextTrimType.NoTrim,
                        LeftButtonAction = () => ShowSettings(),
                        LeftButtonContent = "Настройки",
                        Icon = new BitmapImage(new Uri("pack://application:,,,/res/virustotal.png")),
                    }, expirationTime: TimeSpan.FromSeconds(5));
                }
                StringCollection ToRemove = new();
                foreach (string? path in AutoTotal.Properties.Settings.Default.Folders) {
                    if (!Directory.Exists(path)) {
                        ToRemove.Add(path);
                        Data.notificationManager.Show(new NotificationContent {
                            Title = "Несуществующая папка была удалена из списка",
                            Message = path,
                            Type = NotificationType.Information,
                            TrimType = NotificationTextTrimType.NoTrim,
                            LeftButtonAction = () => ShowSettings(),
                            LeftButtonContent = "Настройки"
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