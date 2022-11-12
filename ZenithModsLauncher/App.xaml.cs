using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ZenithModsLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => ShowExceptionMessage(e.ExceptionObject);

            var args = Environment.GetCommandLineArgs();
            switch (Environment.GetCommandLineArgs().ElementAtOrDefault(1))
            {
                case "--vtz.update":
                    var oldFile = args[2];

                    Cleanup(oldFile, args[3]);

                    try
                    {
                        var currentFile = Assembly.GetExecutingAssembly().Location;
                        File.Copy(currentFile, oldFile, true);
                        Process.Start(oldFile, $"--vtz.update2 \"{currentFile}\" \"{Process.GetCurrentProcess().Id}\"");
                    }
                    catch (Exception ex)
                    {
                        ShowExceptionMessage(ex);
                    }
                    Environment.Exit(0);
                    break;
                case "--vtz.update2":
                    var tempFile = args[2];

                    Cleanup(tempFile, args[3]);
                    break;
            }
        }

        private static void Cleanup(string file, string pid)
        {
            try
            {
                Process process = null;
                try
                {
                    process = Process.GetProcessById(int.Parse(pid));
                }
                // Process is not running
                catch (ArgumentException) { }
                if (process != null)
                {
                    process.Kill();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                ShowExceptionMessage(ex);
            }

            for (int i = 0; i < 10; i++)
            {
                try { File.Delete(file); } catch { Thread.Sleep(500); continue; }
                break;
            }

            if (File.Exists(file))
                ShowExceptionMessage(new IOException());
        }

        public static void ShowExceptionMessage(object exception)
        {
            MessageBox.Show("Uh-oh! Произошло что-то очень плохое...\n" +
                            "Скорее сообщи разработчику эту информацию:\n\n" +
                            (exception?.ToString() ?? "Unknown Exception"), "");
        }
    }
}
