using Microsoft.Win32;

namespace AutoTotal {
    internal static class Autorun {
        public static void Add() {
            using RegistryKey run = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!;
            run.SetValue("AutoTotal", AppDomain.CurrentDomain.BaseDirectory + "AutoTotal.exe /autorun");
        }

        public static void Remove() {
            using RegistryKey run = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!;
            run.DeleteValue("AutoTotal", false);
        }

        public static bool Exists() {
            using RegistryKey run = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false)!;
            return !string.IsNullOrEmpty(run.GetValue("AutoTotal") as string);
        }
    }
}