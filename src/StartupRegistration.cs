using System;
using System.IO;
using Microsoft.Win32;

namespace ThreeCats
{
    public sealed class StartupRegistration
    {
        public const string ValueName = "ThreeCatsDesktopPet";
        private readonly string registryPath;

        public StartupRegistration() : this("Software\\Microsoft\\Windows\\CurrentVersion\\Run") { }
        public StartupRegistration(string path) { registryPath = path; }

        public bool IsEnabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                    return key != null && !string.IsNullOrWhiteSpace(key.GetValue(ValueName) as string);
            }
        }

        public void SetEnabled(bool enabled, string executablePath)
        {
            if (enabled)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(registryPath))
                    key.SetValue(ValueName, "\"" + Path.GetFullPath(executablePath) + "\"", RegistryValueKind.String);
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                    if (key != null) key.DeleteValue(ValueName, false);
            }
        }
    }
}
