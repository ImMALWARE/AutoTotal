using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
namespace AutoTotal {
    public partial class SettingsWindow : Window {
        public SettingsWindow() {
            InitializeComponent();
            foreach (string path in Properties.Settings.Default.Folders?.OfType<string>() ?? Enumerable.Empty<string>()) Folders.Items.Add(new TextBlock { Text = path });
            BlockCheckbox.IsChecked = Properties.Settings.Default.BlockFiles;
            AutoRunCheckbox.IsChecked = Autorun.Exists();
        }

        private void AddFolder(object sender, RoutedEventArgs e) {
            using var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.SelectedPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", string.Empty)!.ToString()!;
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath)) {
                if (!Properties.Settings.Default.Folders.Contains(folderBrowserDialog.SelectedPath)) {
                    Properties.Settings.Default.Folders.Add(folderBrowserDialog.SelectedPath);
                    Properties.Settings.Default.Save();
                    FolderSpy.Add(folderBrowserDialog.SelectedPath);
                    Folders.Items.Add(new TextBlock() { Text = folderBrowserDialog.SelectedPath });
                }
            }
        }

        private void DelFolder(object sender, RoutedEventArgs e) {
            FolderSpy.Remove((Folders.SelectedItem as TextBlock)?.Text);
            Properties.Settings.Default.Folders.Remove((Folders.SelectedItem as TextBlock)?.Text);
            Properties.Settings.Default.Save();
            Folders.Items.Remove(Folders.SelectedItem);
        }

        private void Save(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.BlockFiles = BlockCheckbox?.IsChecked ?? false;
            if (AutoRunCheckbox.IsChecked ?? false) {
                if (!Autorun.Exists()) Autorun.Add();
            }
            else if (Autorun.Exists()) Autorun.Remove();
            Properties.Settings.Default.Save();
            Close();
        }

        private void ChangeVTKey(object sender, RoutedEventArgs e) {
            new SetKeyWindow().Show();
        }
    }
}