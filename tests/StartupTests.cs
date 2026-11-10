using System;
using Microsoft.Win32;
using ThreeCats;
class StartupTests
{
    static int count;
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        count++;
        Console.WriteLine("PASS " + label);
    }
    static void Main()
    {
        string path = "Software\\ThreeCatsDesktopPet.Tests-" + Guid.NewGuid().ToString("N");
        try
        {
            var startup = new StartupRegistration(path);
            Check(!startup.IsEnabled, "startup defaults off without registration");
            startup.SetEnabled(true, "C:\\Music Pets\\三猫桌宠.exe");
            Check(startup.IsEnabled, "startup can be enabled");
            using (var key = Registry.CurrentUser.OpenSubKey(path, true))
            {
                Check((string)key.GetValue(StartupRegistration.ValueName) == "\"C:\\Music Pets\\三猫桌宠.exe\"", "startup command quotes paths with spaces");
                key.SetValue("UnrelatedSetting", "retained");
            }
            startup.SetEnabled(true, "D:\\Pets\\三猫桌宠.exe");
            using (var key = Registry.CurrentUser.OpenSubKey(path))
                Check((string)key.GetValue(StartupRegistration.ValueName) == "\"D:\\Pets\\三猫桌宠.exe\"", "startup path can follow moved portable executable");
            startup.SetEnabled(false, "D:\\Pets\\三猫桌宠.exe");
            Check(!startup.IsEnabled, "startup can be disabled");
            using (var key = Registry.CurrentUser.OpenSubKey(path))
                Check((string)key.GetValue("UnrelatedSetting") == "retained", "disabling startup only removes owned value");
            Console.WriteLine(count + " startup checks passed");
        }
        catch (Exception exception) { Console.WriteLine("FAIL " + exception.Message); Environment.ExitCode = 1; }
        finally { Registry.CurrentUser.DeleteSubKey(path, false); }
    }
}
