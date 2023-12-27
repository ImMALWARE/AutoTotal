using Microsoft.Win32;
using System.IO;

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

    internal static class Blocker {

        public static void Block(string path) {
            File.WriteAllText(path + ":Zone.Identifier:$DATA", "[ZoneTransfer]\nZoneId=4");
        }

        public static void Unblock(string path) {
            File.Delete(path + ":Zone.Identifier:$DATA");
        }
    }
}
