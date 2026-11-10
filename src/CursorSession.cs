using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ThreeCats
{
    internal sealed class CursorSession : IDisposable
    {
        internal const string WorkerArgument = "--cursor-session";
        private Process worker;

        internal void Select(string theme)
        {
            if (worker != null && worker.HasExited) Dispose();
            if (worker == null)
            {
                if (theme == "off") return;
                worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath, WorkerArgument)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true
                });
            }

            try
            {
                worker.StandardInput.WriteLine(theme);
                var response = worker.StandardOutput.ReadLineAsync();
                if (!response.Wait(5000) || response.Result != "ok")
                    throw new IOException("Windows could not change the cursor scheme.");
            }
            catch
            {
                // Closing the pipe also requests recovery when a command fails.
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (worker == null) return;
            try
            {
                try { worker.StandardInput.Close(); }
                catch (IOException) { }
                worker.WaitForExit(2000);
            }
            finally
            {
                worker.Dispose();
                worker = null;
            }
        }

        internal static void RunWorker()
        {
            using (var mutex = new Mutex(false, @"Local\ThreeCatsDesktopPet.CursorSession.v1"))
            {
                try { if (!mutex.WaitOne(4000)) return; }
                catch (AbandonedMutexException) { }
                try { ReadCommands(); }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static void ReadCommands()
        {
            var scheme = new CursorScheme(CursorScheme.RegistryPath, CursorScheme.ReloadWindows, CursorScheme.ApplyPersonCursor);
            string directory = Path.Combine(Path.GetTempPath(), "ThreeCats.Cursors-" + Guid.NewGuid().ToString("N"));
            bool extracted = false;
            try
            {
                using (var input = new StreamReader(Console.OpenStandardInput(), Encoding.ASCII))
                using (var output = new StreamWriter(Console.OpenStandardOutput(), Encoding.ASCII) { AutoFlush = true })
                {
                    string theme;
                    while ((theme = input.ReadLine()) != null)
                    {
                        if (theme == "off") scheme.Restore();
                        else
                        {
                            if (!CursorScheme.IsTheme(theme)) throw new IOException("Unknown cursor theme.");
                            if (!extracted)
                            {
                                CursorScheme.Extract(directory);
                                extracted = true;
                            }
                            scheme.Apply(Path.Combine(directory, theme));
                        }
                        output.WriteLine("ok");
                    }
                }
            }
            catch (Exception exception)
            {
                if (!IsCursorError(exception)) throw;
            }
            finally
            {
                try { scheme.Restore(); }
                catch (Win32Exception) { }
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        internal static bool IsCursorError(Exception exception)
        {
            var aggregate = exception as AggregateException;
            if (aggregate != null) return aggregate.Flatten().InnerExceptions.All(IsCursorError);
            return exception is IOException || exception is UnauthorizedAccessException
                || exception is Win32Exception || exception is SecurityException;
        }
    }
}
