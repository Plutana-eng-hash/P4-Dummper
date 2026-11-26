using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FluentFTP;

namespace PS4wpfDumper
{
    public partial class PS4DashboardWindow : Window
    {
        private readonly string host;
        private readonly int port;
        private readonly string username;
        private readonly string password;
        private readonly StringBuilder report = new StringBuilder();

        public PS4DashboardWindow(string host, int port, string username, string password)
        {
            InitializeComponent();
            this.host = host; this.port = port; this.username = username; this.password = password;
            networkInfo.Text = "Host: " + host + Environment.NewLine + "FTP port: " + port + Environment.NewLine + "Authentication: using the dumper's configured FTP login";
            connectionLabel.Text = "Target: " + host + ":" + port;
            Loaded += async (s, e) => await RefreshDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) { await RefreshDataAsync(); }

        private async Task RefreshDataAsync()
        {
            refreshButton.IsEnabled = false; exportButton.IsEnabled = false;
            ftpStatus.Text = "Scanning"; ftpStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;
            statusLabel.Text = "Connecting and reading selected information...";
            report.Clear();
            report.AppendLine("PS4 Console Dashboard"); report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("FTP target: " + host + ":" + port);
            var users = new List<string>(); var mounts = new List<string>(); var apps = new List<string>();
            var errors = new List<string>(); string version = "Unavailable (not readable)";
            try
            {
                using (var client = new AsyncFtpClient(host, username, password, port))
                {
                    client.Config.ConnectTimeout = 8000; client.Config.ReadTimeout = 15000;
                    await client.Connect();
                    connectionLabel.Text = "Connected • " + host + ":" + port;
                    ftpStatus.Text = "Online"; ftpStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
                    report.AppendLine("FTP connection: successful");

                    if (scanAccountsCheckBox.IsChecked == true)
                    {
                        try { var l = await client.GetListing("/user/home/", FtpListOption.ForceList); users = l.Where(v => v.Type == FtpObjectType.Directory).Select(v => v.Name.TrimEnd('/')).Where(v => !String.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(); report.AppendLine("Local user directory IDs: " + users.Count); foreach (var id in users) report.AppendLine("  " + id); }
                        catch (Exception ex) { errors.Add("/user/home: " + ex.Message); }
                    }
                    if (scanMountsCheckBox.IsChecked == true)
                    {
                        try { var l = await client.GetListing("/mnt/sandbox/pfsmnt/", FtpListOption.ForceList); mounts = l.Where(v => v.Type == FtpObjectType.Directory).Select(v => v.Name).Where(v => v.EndsWith("-app0", StringComparison.OrdinalIgnoreCase) || v.EndsWith("-patch0", StringComparison.OrdinalIgnoreCase)).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(); report.AppendLine("Game mounts: " + mounts.Count); foreach (var m in mounts) report.AppendLine("  " + m); }
                        catch (Exception ex) { errors.Add("/mnt/sandbox/pfsmnt: " + ex.Message); }
                    }
                    if (scanAppsCheckBox.IsChecked == true)
                    {
                        try { var l = await client.GetListing("/user/appmeta/", FtpListOption.ForceList); apps = l.Where(v => v.Type == FtpObjectType.Directory).Select(v => v.Name).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(); report.AppendLine("App metadata directories: " + apps.Count); }
                        catch (Exception ex) { errors.Add("/user/appmeta: " + ex.Message); }
                    }
                    if (readVersionCheckBox.IsChecked == true)
                    {
                        string temp = Path.Combine(Path.GetTempPath(), "ps4-version-" + Guid.NewGuid().ToString("N") + ".txt");
                        try { var result = await client.DownloadFile(temp, "/etc/version", FtpLocalExists.Overwrite, FtpVerify.None); if (result == FtpStatus.Success && File.Exists(temp)) { version = File.ReadAllText(temp).Trim(); if (String.IsNullOrWhiteSpace(version)) version = "File present but empty"; } }
                        catch (Exception ex) { errors.Add("/etc/version: " + ex.Message); }
                        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
                    }
                    report.AppendLine("System version: " + version);
                    try { await client.Disconnect(); } catch { }
                }
                usersCount.Text = scanAccountsCheckBox.IsChecked == true ? users.Count.ToString() : "Skipped";
                mountsCount.Text = scanMountsCheckBox.IsChecked == true ? mounts.Count.ToString() : "Skipped";
                appsCount.Text = scanAppsCheckBox.IsChecked == true ? apps.Count.ToString() : "Skipped";
                accountsList.ItemsSource = users.Select(id => new AccountRow { Id = id, Source = "/user/home" }).ToList();
                consoleInfo.Text = "System version: " + version + Environment.NewLine + "FTP endpoint: " + host + ":" + port + Environment.NewLine + "Connection: successful";
                scanInfo.Text = "Local users: " + (scanAccountsCheckBox.IsChecked == true ? users.Count.ToString() : "not scanned") + Environment.NewLine + "Game mounts: " + (scanMountsCheckBox.IsChecked == true ? mounts.Count.ToString() : "not scanned") + Environment.NewLine + "App metadata: " + (scanAppsCheckBox.IsChecked == true ? apps.Count.ToString() : "not scanned") + Environment.NewLine + "Optional path errors: " + errors.Count;
                report.AppendLine("Optional path errors: " + errors.Count); foreach (var error in errors) report.AppendLine("  " + error);
                statusLabel.Text = errors.Count == 0 ? "Scan complete." : "Connected; some optional paths were unavailable. See exported report.";
            }
            catch (Exception ex)
            {
                ftpStatus.Text = "Offline"; ftpStatus.Foreground = System.Windows.Media.Brushes.Firebrick;
                connectionLabel.Text = "Could not connect to " + host + ":" + port;
                statusLabel.Text = "FTP connection failed: " + ex.Message;
                consoleInfo.Text = "No console information was read."; scanInfo.Text = ex.Message;
                report.AppendLine("FTP connection failed: " + ex);
                MessageBox.Show("Could not connect to the PS4 FTP server.\n\n" + ex.Message, "PS4 connection error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { refreshButton.IsEnabled = true; exportButton.IsEnabled = true; }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export PS4 console report", Filter = "Text report (*.txt)|*.txt", FileName = "PS4-Console-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
            if (dialog.ShowDialog(this) == true) { File.WriteAllText(dialog.FileName, report.ToString(), Encoding.UTF8); statusLabel.Text = "Report saved: " + dialog.FileName; }
        }

        private sealed class AccountRow { public string Id { get; set; } public string Source { get; set; } }
    }
}