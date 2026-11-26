using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using Microsoft.Win32;
using FluentFTP;

namespace PS4wpfDumper
{
    public partial class PS4DashboardWindow : Window
    {
        private readonly string initialHost;
        private readonly int initialPort;
        private readonly string username;
        private readonly string password;
        private AsyncFtpClient ftpClient;
        private readonly StringBuilder report = new StringBuilder();
        private bool busy;
        private string editorRemotePath;

        public PS4DashboardWindow(string host, int port, string username, string password)
        {
            InitializeComponent();
            initialHost = host ?? "";
            initialPort = port;
            this.username = username ?? "";
            this.password = password ?? "";
            hostTextBox.Text = initialHost;
            portTextBox.Text = initialPort.ToString();
            networkInfo.Text = "Target: " + initialHost + ":" + initialPort + Environment.NewLine +
                "FTP user: " + (String.IsNullOrWhiteSpace(this.username) ? "(not configured)" : this.username) +
                Environment.NewLine + "Credentials are used only for this FTP connection.";
            UpdateConnectionState(false, "NOT CONNECTED");
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            int port;
            if (String.IsNullOrWhiteSpace(hostTextBox.Text) ||
                !Int32.TryParse(portTextBox.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Enter a valid PS4 IP address or host name and FTP port.", "Connection details",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "Connecting to PS4 FTP...");
            try
            {
                await DisconnectClientAsync();
                ftpClient = new AsyncFtpClient(hostTextBox.Text.Trim(), username, password, port);
                ftpClient.Config.ConnectTimeout = 8000;
                ftpClient.Config.ReadTimeout = 15000;
                await ftpClient.Connect();
                if (!ftpClient.IsConnected) throw new IOException("The FTP server did not complete the connection.");
                UpdateConnectionState(true, "CONNECTED");
                networkInfo.Text = "Host: " + hostTextBox.Text.Trim() + Environment.NewLine +
                    "FTP port: " + port + Environment.NewLine +
                    "FTP login: " + (String.IsNullOrWhiteSpace(username) ? "anonymous" : username) +
                    Environment.NewLine + "Transport: FTP (read-only dashboard scans)";
                await ScanConnectedConsoleAsync();
                await BrowseRemoteAsync();
            }
            catch (Exception ex)
            {
                await DisconnectClientAsync();
                UpdateConnectionState(false, "CONNECTION FAILED");
                statusLabel.Text = "Connection failed: " + ex.Message;
                report.AppendLine("Connection failed: " + ex);
                MessageBox.Show("Could not connect to the PS4 FTP server. Check the PS4 IP, FTP port, and that the console FTP server is running.\n\n" + ex.Message,
                    "PS4 connection error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetBusy(false, statusLabel.Text); }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            if (ftpClient == null || !ftpClient.IsConnected)
            {
                statusLabel.Text = "Not connected. Press Connect to PS4 first.";
                return;
            }
            SetBusy(true, "Refreshing console information...");
            try { await ScanConnectedConsoleAsync(); }
            catch (Exception ex) { statusLabel.Text = "Refresh failed: " + ex.Message; }
            finally { SetBusy(false, statusLabel.Text); }
        }

        private async Task ScanConnectedConsoleAsync()
        {
            report.Clear();
            report.AppendLine("PS4 Console Dashboard Report");
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("FTP target: " + hostTextBox.Text.Trim() + ":" + portTextBox.Text);
            var errors = new List<string>();
            var users = new List<string>();
            var mounts = new List<string>();
            var apps = new List<string>();
            var roots = new List<string>();
            string version = "Unavailable / permission denied";

            try
            {
                var rootListing = await ftpClient.GetListing("/", FtpListOption.ForceList);
                roots = rootListing.Where(x => x.Type == FtpObjectType.Directory)
                    .Select(x => x.Name.TrimEnd('/')).Where(x => !String.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                report.AppendLine("Root directories: " + String.Join(", ", roots));
            }
            catch (Exception ex) { errors.Add("/: " + ex.Message); }

            if (scanAccountsCheckBox.IsChecked == true)
            {
                try
                {
                    var listing = await ftpClient.GetListing("/user/home/", FtpListOption.ForceList);
                    users = listing.Where(x => x.Type == FtpObjectType.Directory)
                        .Select(x => x.Name.TrimEnd('/')).Where(x => !String.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    report.AppendLine("Local user directory IDs (" + users.Count + "):");
                    foreach (var id in users) report.AppendLine("  " + id);
                }
                catch (Exception ex) { errors.Add("/user/home/: " + ex.Message); }
            }

            if (scanMountsCheckBox.IsChecked == true)
            {
                try
                {
                    var listing = await ftpClient.GetListing("/mnt/sandbox/pfsmnt/", FtpListOption.ForceList);
                    mounts = listing.Where(x => x.Type == FtpObjectType.Directory)
                        .Select(x => x.Name.TrimEnd('/'))
                        .Where(x => x.EndsWith("-app0", StringComparison.OrdinalIgnoreCase) ||
                                    x.EndsWith("-patch0", StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    report.AppendLine("Game mount directories (" + mounts.Count + "):");
                    foreach (var name in mounts) report.AppendLine("  " + name);
                }
                catch (Exception ex) { errors.Add("/mnt/sandbox/pfsmnt/: " + ex.Message); }
            }

            if (scanAppsCheckBox.IsChecked == true)
            {
                try
                {
                    var listing = await ftpClient.GetListing("/user/appmeta/", FtpListOption.ForceList);
                    apps = listing.Where(x => x.Type == FtpObjectType.Directory)
                        .Select(x => x.Name.TrimEnd('/')).Where(x => !String.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    report.AppendLine("App metadata directories (" + apps.Count + "):");
                    foreach (var name in apps) report.AppendLine("  " + name);
                }
                catch (Exception ex) { errors.Add("/user/appmeta/: " + ex.Message); }
            }

            if (readVersionCheckBox.IsChecked == true)
            {
                string temp = Path.Combine(Path.GetTempPath(), "ps4-version-" + Guid.NewGuid().ToString("N") + ".txt");
                try
                {
                    var result = await ftpClient.DownloadFile(temp, "/etc/version", FtpLocalExists.Overwrite, FtpVerify.None);
                    if (result == FtpStatus.Success && File.Exists(temp))
                    {
                        version = File.ReadAllText(temp).Trim();
                        if (String.IsNullOrWhiteSpace(version)) version = "File present but empty";
                    }
                }
                catch (Exception ex) { errors.Add("/etc/version: " + ex.Message); }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }

            usersCount.Text = scanAccountsCheckBox.IsChecked == true ? users.Count.ToString() : "—";
            mountsCount.Text = scanMountsCheckBox.IsChecked == true ? mounts.Count.ToString() : "—";
            appsCount.Text = scanAppsCheckBox.IsChecked == true ? apps.Count.ToString() : "—";
            versionValue.Text = readVersionCheckBox.IsChecked == true ? version : "Skipped";
            rootList.ItemsSource = roots;
            accountsList.ItemsSource = users.Select(id => new AccountRow { Id = id, Source = "/user/home" }).ToList();
            mountsList.ItemsSource = mounts;
            appsList.ItemsSource = apps;

            report.AppendLine("System version: " + version);
            report.AppendLine("Optional scan errors (" + errors.Count + "):");
            foreach (var error in errors) report.AppendLine("  " + error);
            report.AppendLine();
            report.AppendLine("Privacy: only directory names and the optional public system version file are read. PSN identity, email, passwords, tokens, saves and licenses are not queried.");

            statusLabel.Text = errors.Count == 0
                ? "Connected. Scan complete • " + DateTime.Now.ToString("HH:mm:ss")
                : "Connected with " + errors.Count + " unavailable path(s). Check report for details.";
            UpdateConnectionState(true, "CONNECTED");
            exportButton.IsEnabled = true;
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            SetBusy(true, "Disconnecting...");
            await DisconnectClientAsync();
            UpdateConnectionState(false, "DISCONNECTED");
            statusLabel.Text = "Disconnected from PS4.";
            SetBusy(false, statusLabel.Text);
        }

        private async Task DisconnectClientAsync()
        {
            var old = ftpClient;
            ftpClient = null;
            if (old != null)
            {
                try { if (old.IsConnected) await old.Disconnect(); } catch { }
                try { old.Dispose(); } catch { }
            }
        }

        private void UpdateConnectionState(bool connected, string label)
        {
            connectionLabel.Text = label;
            statusDot.Fill = connected ? Brushes.White : Brushes.Gray;
            connectButton.IsEnabled = !connected;
            refreshButton.IsEnabled = connected;
            disconnectButton.IsEnabled = connected;
            hostTextBox.IsEnabled = !connected;
            portTextBox.IsEnabled = !connected;
            if (!connected) exportButton.IsEnabled = report.Length > 0;
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            connectButton.IsEnabled = !value && (ftpClient == null || !ftpClient.IsConnected);
            refreshButton.IsEnabled = !value && ftpClient != null && ftpClient.IsConnected;
            disconnectButton.IsEnabled = !value && ftpClient != null && ftpClient.IsConnected;
            hostTextBox.IsEnabled = !value && (ftpClient == null || !ftpClient.IsConnected);
            portTextBox.IsEnabled = !value && (ftpClient == null || !ftpClient.IsConnected);
            statusLabel.Text = message;
        }


        private sealed class RemoteFileRow { public string Name {get;set;} public string FullPath {get;set;} public bool IsDirectory {get;set;} public string TypeLabel {get{return IsDirectory?"Folder":"File";}} public string SizeLabel {get;set;} }
        private string RemotePath { get { var p=(filePathBox.Text??"/").Replace('\\','/').Trim(); if(!p.StartsWith("/"))p="/"+p; return p.Length>1?p.TrimEnd('/'):"/"; } }
        private async Task BrowseRemoteAsync() {
            if(ftpClient==null||!ftpClient.IsConnected) throw new IOException("Connect to PS4 first.");
            string p=RemotePath; var items=await ftpClient.GetListing(p,FtpListOption.ForceList);
            remoteFilesList.ItemsSource=items.Where(i=>i.Name!="."&&i.Name!="..").OrderByDescending(i=>i.Type==FtpObjectType.Directory).ThenBy(i=>i.Name,StringComparer.OrdinalIgnoreCase).Select(i=>new RemoteFileRow{Name=i.Name,FullPath=p=="/"?"/"+i.Name:p+"/"+i.Name,IsDirectory=i.Type==FtpObjectType.Directory,SizeLabel=i.Type==FtpObjectType.Directory?"—":i.Size.ToString("N0")+" bytes"}).ToList();
            filePathBox.Text=p; fileExplorerStatus.Text=remoteFilesList.Items.Count+" items • "+p;
        }
        private async void FileGo_Click(object s,RoutedEventArgs e){await SafeBrowseAsync();}
        private async void FileRefresh_Click(object s,RoutedEventArgs e){await SafeBrowseAsync();}
        private async void FileUp_Click(object s,RoutedEventArgs e){string p=RemotePath; int n=p.LastIndexOf('/'); filePathBox.Text=n<=0?"/":p.Substring(0,n); await SafeBrowseAsync();}
        private async Task SafeBrowseAsync(){try{await BrowseRemoteAsync();}catch(Exception ex){fileExplorerStatus.Text=ex.Message;}}
        private async void RemoteFilesList_MouseDoubleClick(object s,System.Windows.Input.MouseButtonEventArgs e){
            var r=remoteFilesList.SelectedItem as RemoteFileRow;if(r==null)return;
            if(r.IsDirectory){filePathBox.Text=r.FullPath;await SafeBrowseAsync();return;}
            string tmp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".txt");
            try{var info=await ftpClient.GetObjectInfo(r.FullPath);if(info!=null&&info.Size>2097152)throw new IOException("Text editor limit is 2 MB; use Download.");
            var status=await ftpClient.DownloadFile(tmp,r.FullPath,FtpLocalExists.Overwrite,FtpVerify.None);if(status!=FtpStatus.Success)throw new IOException("Download failed.");
            var bytes=File.ReadAllBytes(tmp);if(bytes.Any(b=>b==0))throw new IOException("Binary file: use Download instead.");
            fileEditor.Text=new UTF8Encoding(false,true).GetString(bytes);fileEditor.IsEnabled=true;editorRemotePath=r.FullPath;fileExplorerStatus.Text="Editing "+r.FullPath;
            }catch(Exception ex){fileExplorerStatus.Text=ex.Message;}finally{try{if(File.Exists(tmp))File.Delete(tmp);}catch{}}
        }
        private async void FileSave_Click(object s,RoutedEventArgs e){
            if(String.IsNullOrEmpty(editorRemotePath)){fileExplorerStatus.Text="Open a text file first.";return;}
            string tmp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".txt");
            try{File.WriteAllText(tmp,fileEditor.Text,new UTF8Encoding(false));var result=await ftpClient.UploadFile(tmp,editorRemotePath,FtpRemoteExists.Overwrite,true,FtpVerify.None);if(result!=FtpStatus.Success)throw new IOException("Upload failed.");fileExplorerStatus.Text="Saved "+editorRemotePath;await BrowseRemoteAsync();}
            catch(Exception ex){fileExplorerStatus.Text=ex.Message;}finally{try{if(File.Exists(tmp))File.Delete(tmp);}catch{}}
        }
        private async void FileUpload_Click(object s,RoutedEventArgs e){
            var d=new OpenFileDialog();if(d.ShowDialog(this)!=true)return;
            try{string target=RemotePath.TrimEnd('/')+"/"+Path.GetFileName(d.FileName);var result=await ftpClient.UploadFile(d.FileName,target,FtpRemoteExists.Overwrite,true,FtpVerify.None);if(result!=FtpStatus.Success)throw new IOException("Upload failed.");await BrowseRemoteAsync();}
            catch(Exception ex){fileExplorerStatus.Text=ex.Message;}
        }
        private async void FileDownload_Click(object s,RoutedEventArgs e){
            var r=remoteFilesList.SelectedItem as RemoteFileRow;if(r==null||r.IsDirectory)return;var d=new SaveFileDialog{FileName=r.Name};if(d.ShowDialog(this)!=true)return;
            try{var result=await ftpClient.DownloadFile(d.FileName,r.FullPath,FtpLocalExists.Overwrite,FtpVerify.None);if(result!=FtpStatus.Success)throw new IOException("Download failed.");fileExplorerStatus.Text="Downloaded "+d.FileName;}catch(Exception ex){fileExplorerStatus.Text=ex.Message;}
        }
        private string PromptForFolderName()
        {
            var dialog = new Window
            {
                Title = "Create PS4 folder",
                Width = 360,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                Foreground = Brushes.White
            };
            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Folder name", Margin = new Thickness(0, 0, 0, 8) });
            var nameBox = new System.Windows.Controls.TextBox { Text = "NewFolder", Padding = new Thickness(6) };
            panel.Children.Add(nameBox);
            var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var create = new System.Windows.Controls.Button { Content = "Create", Width = 82, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 82, IsCancel = true };
            create.Click += (sender, args) => { dialog.DialogResult = true; };
            buttons.Children.Add(create);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            nameBox.Focus();
            nameBox.SelectAll();
            return dialog.ShowDialog() == true ? nameBox.Text.Trim() : null;
        }

        private async void FileNewFolder_Click(object s, RoutedEventArgs e)
        {
            if (ftpClient == null || !ftpClient.IsConnected) { fileExplorerStatus.Text = "Connect to PS4 first."; return; }
            string name = PromptForFolderName();
            if (String.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
                name.IndexOfAny(new[] { '/', '\\' }) >= 0) return;
            try
            {
                string target = RemotePath == "/" ? "/" + name : RemotePath + "/" + name;
                await ftpClient.CreateDirectory(target);
                await BrowseRemoteAsync();
                fileExplorerStatus.Text = "Created " + target;
            }
            catch (Exception ex) { fileExplorerStatus.Text = ex.Message; }
        }

        private async void FileDelete_Click(object s,RoutedEventArgs e){
            var r=remoteFilesList.SelectedItem as RemoteFileRow;if(r==null)return;
            if(MessageBox.Show("Delete "+r.Name+" from the PS4?","Confirm delete",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            try{if(r.IsDirectory)await ftpClient.DeleteDirectory(r.FullPath);else await ftpClient.DeleteFile(r.FullPath);await BrowseRemoteAsync();}catch(Exception ex){fileExplorerStatus.Text=ex.Message;}
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export PS4 console report",
                Filter = "Text report (*.txt)|*.txt",
                FileName = "PS4-Console-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"
            };
            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, report.ToString(), Encoding.UTF8);
                statusLabel.Text = "Report exported to " + dialog.FileName;
            }
        }

        private void OpenDumperButton_Click(object sender, RoutedEventArgs e)
        {
            var dumper = new MainWindow();
            int port;
            if (!Int32.TryParse(portTextBox.Text, out port)) port = 2121;
            dumper.ApplyConnectionSettings(hostTextBox.Text.Trim(), port, username, password);
            dumper.Owner = this;
            dumper.Show();
            statusLabel.Text = "Opened PS4 Dumper with the current connection settings.";
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectClientAsync();
            Close();
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ftpClient != null) await DisconnectClientAsync();
        }

        private sealed class AccountRow
        {
            public string Id { get; set; }
            public string Source { get; set; }
        }
    }
}