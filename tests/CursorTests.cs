using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ThreeCats;

static class CursorTests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string file, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern bool DestroyCursor(IntPtr cursor);

    static int Main(string[] args)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThreeCats.CursorTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            CursorScheme.Extract(directory);
            string[] roles =
            {
                "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No",
                "SizeNS", "SizeWE", "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person"
            };
            foreach (string theme in new[] { "wand", "treat", "catnip" })
                CheckThinIBeam(Path.Combine(directory, theme, "IBeam.cur"));
            int loaded = 0;
            foreach (string theme in new[] { "wand", "treat", "catnip" })
                foreach (string role in roles)
                {
                    string extension = role == "Wait" || role == "AppStarting" ? ".ani" : ".cur";
                    string file = Path.Combine(directory, theme, role + extension);
                    var handle = LoadImage(IntPtr.Zero, file, 2, 32, 32, 0x10);
                    if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), file);
                    DestroyCursor(handle);
                    loaded++;
                }
            Console.WriteLine("PASS Windows loaded all " + loaded + " cursor assets");
            if (args.Length > 0) CheckRegistry(Path.Combine(directory, "wand"), Path.Combine(directory, "catnip"));
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("FAIL " + exception);
            return 1;
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void CheckThinIBeam(string file)
    {
        byte[] data = File.ReadAllBytes(file);
        if (BitConverter.ToUInt16(data, 2) != 2 || BitConverter.ToUInt16(data, 4) != 3)
            throw new Exception("Invalid IBeam directory " + file);
        for (int entry = 0; entry < 3; entry++)
        {
            int size = new[] { 32, 48, 64 }[entry];
            int directory = 6 + entry * 16;
            int offset = BitConverter.ToInt32(data, directory + 12);
            if (data[directory] != size || data[directory + 1] != size ||
                BitConverter.ToUInt16(data, directory + 4) != size / 2 ||
                BitConverter.ToUInt16(data, directory + 6) != size / 2 ||
                BitConverter.ToInt32(data, offset) != 40 ||
                BitConverter.ToInt32(data, offset + 4) != size ||
                BitConverter.ToInt32(data, offset + 8) != size * 2 ||
                BitConverter.ToUInt16(data, offset + 14) != 32)
                throw new Exception("Invalid IBeam image or hotspot " + file);
            int visible = 0;
            for (int x = 0; x < size; x++)
                if (data[offset + 40 + ((size / 2) * size + x) * 4 + 3] > 128) visible++;
            int maximum = size == 64 ? 3 : 2;
            if (visible < 1 || visible > maximum)
                throw new Exception("IBeam stem must be thin at " + size + "px, got " + visible + " in " + file);
        }
        Console.WriteLine("PASS thin IBeam sizes and hotspots " + file);
    }

    private static void CheckRegistry(string first, string second)
    {
        string path = @"Software\ThreeCats.CursorTests-" + Guid.NewGuid().ToString("N");
        const string originalArrow = @"%SystemRoot%\cursors\custom.cur";
        using (var key = Registry.CurrentUser.CreateSubKey(path))
        try
        {
            key.SetValue("Arrow", originalArrow, RegistryValueKind.ExpandString);
            key.SetValue("Wait", "");
            string expected = first;
            int reloads = 0;
            int personLoads = 0;
            var scheme = new CursorScheme(path, delegate
            {
                reloads++;
                string arrow = expected == null ? originalArrow : Path.Combine(expected, "Arrow.cur");
                if ((string)key.GetValue("Arrow", null, RegistryValueOptions.DoNotExpandEnvironmentNames) != arrow)
                    throw new Exception("The reload saw the wrong arrow cursor.");
                if (expected != null && (string)key.GetValue("Person") != Path.Combine(expected, "Person.cur"))
                    throw new Exception("The reload omitted the person cursor.");
            }, delegate(string person)
            {
                if (person != Path.Combine(expected, "Person.cur"))
                    throw new Exception("The dedicated person cursor replacement loaded the wrong theme.");
                personLoads++;
            });

            scheme.Apply(first);
            expected = second;
            scheme.Apply(second);
            expected = null;
            scheme.Restore();
            scheme.Restore();
            if (reloads != 3 || personLoads != 2 || key.GetValueKind("Arrow") != RegistryValueKind.ExpandString ||
                (string)key.GetValue("Wait") != "" || key.GetValue("Person") != null)
                throw new Exception("Switching or restoring changed the saved cursor configuration.");
            Console.WriteLine("PASS switching twice and restoring preserves expanded paths, empty values and missing roles");

            var failingScheme = new CursorScheme(path, delegate { }, delegate { throw new IOException("Simulated replacement failure."); });
            try
            {
                failingScheme.Apply(first);
                throw new Exception("The replacement failure did not propagate.");
            }
            catch (IOException)
            {
                if ((string)key.GetValue("Arrow", null, RegistryValueOptions.DoNotExpandEnvironmentNames) != originalArrow ||
                    key.GetValue("Person") != null)
                    throw new Exception("A failed replacement left cursor paths behind.");
            }
            Console.WriteLine("PASS failed replacement rolls back the temporary registry values");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path); }
    }
}
