using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ThreeCats
{
    // The registry remains the source of truth for the user's own cursor scheme.
    internal sealed class CursorScheme
    {
        internal static readonly string[] Roles =
        {
            "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No",
            "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person"
        };

        internal const string RegistryPath = @"Control Panel\Cursors";
        private readonly string registryPath;
        private readonly Action reload;
        private readonly Action<string> applyPerson;
        private bool active;

        internal CursorScheme(string registryPath, Action reload, Action<string> applyPerson)
        {
            this.registryPath = registryPath;
            this.reload = reload;
            this.applyPerson = applyPerson;
        }

        internal static bool IsTheme(string name)
        {
            return name == "wand" || name == "treat" || name == "catnip";
        }

        internal static string AssetName(string role)
        {
            return role + (role == "Wait" || role == "AppStarting" ? ".ani" : ".cur");
        }

        internal static void Extract(string directory)
        {
            using (var resource = typeof(CursorScheme).Assembly.GetManifestResourceStream("ThreeCats.Cursors.zip"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            {
                foreach (string theme in new[] { "wand", "treat", "catnip" })
                {
                    string folder = Path.Combine(directory, theme);
                    Directory.CreateDirectory(folder);
                    foreach (string role in Roles)
                    {
                        string name = AssetName(role);
                        using (var input = archive.GetEntry(theme + "/" + name).Open())
                        using (var output = File.Create(Path.Combine(folder, name)))
                            input.CopyTo(output);
                    }
                }
            }
        }

        internal void Apply(string directory)
        {
            // Text selection uses the user's saved Windows cursor scheme.
            string[] roles = Array.FindAll(Roles, role => role != "IBeam");
            var paths = new string[roles.Length];
            var originals = new object[roles.Length];
            var kinds = new RegistryValueKind[roles.Length];
            for (int i = 0; i < roles.Length; i++)
            {
                paths[i] = Path.Combine(directory, AssetName(roles[i]));
                if (!File.Exists(paths[i])) throw new FileNotFoundException("Missing cursor asset.", paths[i]);
            }

            using (var key = Registry.CurrentUser.OpenSubKey(registryPath, true))
            {
                if (key == null) throw new IOException("The current user's cursor settings could not be opened.");
                for (int i = 0; i < roles.Length; i++)
                {
                    originals[i] = Read(key, roles[i]);
                    if (originals[i] != null) kinds[i] = key.GetValueKind(roles[i]);
                }

                int written = 0;
                try
                {
                    for (; written < roles.Length; written++)
                        key.SetValue(roles[written], paths[written], RegistryValueKind.String);
                    active = true;
                    reload();
                    applyPerson(Path.Combine(directory, "Person.cur"));
                }
                finally
                {
                    // Windows has loaded the theme. Put the original values back immediately,
                    // keeping the active cursor images until the next reload.
                    for (int i = 0; i < written; i++)
                    {
                        if (!object.Equals(Read(key, roles[i]), paths[i])) continue;
                        if (originals[i] == null) key.DeleteValue(roles[i], false);
                        else key.SetValue(roles[i], originals[i], kinds[i]);
                    }
                }
            }
        }

        internal void Restore()
        {
            if (!active) return;
            reload();
            active = false;
        }

        private static object Read(RegistryKey key, string role)
        {
            return key.GetValue(role, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr instance, string path, uint type, int width, int height, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetSystemCursor(IntPtr cursor, uint role);
        [DllImport("user32.dll")]
        private static extern bool DestroyCursor(IntPtr cursor);

        internal static void ReloadWindows()
        {
            const uint reloadCursors = 0x0057;
            if (!SystemParametersInfo(reloadCursors, 0, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        internal static void ApplyPersonCursor(string path)
        {
            const uint cursorImage = 2;
            const uint loadFromFileAtDefaultSize = 0x10 | 0x40;
            const uint personSelect = 32672;
            // A scheme reload can leave this role unchanged even with a valid Person entry.
            var cursor = LoadImage(IntPtr.Zero, path, cursorImage, 0, 0, loadFromFileAtDefaultSize);
            if (cursor == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (SetSystemCursor(cursor, personSelect)) return;
            int error = Marshal.GetLastWin32Error();
            DestroyCursor(cursor);
            throw new Win32Exception(error);
        }
    }
}
