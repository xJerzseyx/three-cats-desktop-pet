using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle(ThreeCats.Program.AppName)]
[assembly: AssemblyProduct(ThreeCats.Program.AppName)]

namespace ThreeCats
{
    static class Program
    {
        internal const string AppName = "伊二三万";

        [STAThread]
        static void Main()
        {
            bool first;
            using (var mutex = new Mutex(true, "Local\\ThreeCatsDesktopPet.v1", out first))
            {
                if (!first)
                {
                    Native.PostMessage(new IntPtr(0xffff), PetForm.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                try
                {
                    Native.EnableDpi();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new PetForm(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "三猫桌宠设置.xml")));
                }
                catch (Exception exception)
                {
                    MessageBox.Show("桌宠启动或运行遇到问题。\n\n" + exception.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
