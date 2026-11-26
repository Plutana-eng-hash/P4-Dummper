using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
namespace PS4wpfDumper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // WPF and the dumper use relative paths for bins, libs, ext and output.
            // Always resolve them from the installed EXE directory, not the caller's
            // current directory (which is often System32 for shortcuts or launchers).
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            DispatcherUnhandledException += (sender, e) =>
            {
                LogException("Unhandled WPF exception", e.Exception);
                MessageBox.Show(
                    "PS4 Dumper hit an error and could not continue.\n\n" +
                    e.Exception.Message + "\n\nA full log was saved to:\n" + GetLogPath(),
                    "PS4 Dumper startup/runtime error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                if (exception != null)
                    LogException("Unhandled application exception", exception);
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException("Unobserved task exception", e.Exception);
                e.SetObserved();
            };
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var dashboard = new PS4DashboardWindow("10.0.0.6", 2121, "root", "root");
            MainWindow = dashboard;
            dashboard.Show();
        }

        private static string GetLogPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PS4Dumper", "startup.log");
        }

        public static void LogException(string context, Exception exception)
        {
            try
            {
                string path = GetLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                    context + Environment.NewLine +
                    exception + Environment.NewLine +
                    new string('-', 72) + Environment.NewLine);
            }
            catch
            {
                // Logging must never cause a second startup failure.
            }
        }
    }
}
