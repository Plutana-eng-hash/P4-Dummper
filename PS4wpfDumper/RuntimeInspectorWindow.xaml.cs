using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using Microsoft.Win32;

namespace PS4wpfDumper
{
    public partial class RuntimeInspectorWindow : Window
    {
        private readonly ObservableCollection<RuntimeAddressRecord> records = new ObservableCollection<RuntimeAddressRecord>();
        private TcpListener listener;
        private CancellationTokenSource cancellation;

        public RuntimeInspectorWindow()
        {
            InitializeComponent();
            recordsGrid.ItemsSource = records;
        }

        private async void listenButton_Click(object sender, RoutedEventArgs e)
        {
            if (listener != null)
            {
                StopListener();
                return;
            }

            int port;
            if (!int.TryParse(portTextBox.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Enter a valid TCP port between 1 and 65535.", "Invalid port", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                cancellation = new CancellationTokenSource();
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start(4);
                listenButton.Content = "Stop listener";
                portTextBox.IsEnabled = false;
                statusText.Text = "Listening on TCP " + port;
                await AcceptClientsAsync(cancellation.Token);
            }
            catch (ObjectDisposedException) { }
            catch (SocketException ex)
            {
                statusText.Text = "Listener error: " + ex.Message;
                StopListener();
            }
            catch (Exception ex)
            {
                statusText.Text = "Error: " + ex.Message;
                StopListener();
            }
        }

        private async Task AcceptClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && listener != null)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Task.Run(() => ReceiveClientAsync(client, token));
            }
        }

        private async Task ReceiveClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    string line;
                    try { line = await reader.ReadLineAsync(); }
                    catch { break; }
                    if (line == null) break;
                    if (line.Length > 65536)
                    {
                        Dispatcher.BeginInvoke(new Action(() => statusText.Text = "Rejected oversized record."));
                        break;
                    }

                    RuntimeAddressRecord record;
                    string error;
                    if (TryParseRecord(line, out record, out error))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            records.Add(record);
                            saveButton.IsEnabled = records.Count > 0;
                            statusText.Text = "Received " + records.Count + " record(s).";
                        }));
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new Action(() => statusText.Text = "Rejected record: " + error));
                    }
                }
            }
        }

        private static bool TryParseRecord(string json, out RuntimeAddressRecord record, out string error)
        {
            record = null;
            error = null;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 65536 };
                var data = serializer.Deserialize<Dictionary<string, object>>(json);
                string module = GetString(data, "moduleName");
                string baseText = GetString(data, "moduleBase");
                string addressText = GetString(data, "address");
                ulong moduleBase, address;
                if (String.IsNullOrWhiteSpace(module)) throw new FormatException("moduleName is required");
                if (!TryAddress(baseText, out moduleBase)) throw new FormatException("moduleBase must be a hexadecimal or decimal address");
                if (!TryAddress(addressText, out address)) throw new FormatException("address must be a hexadecimal or decimal address");
                if (address < moduleBase) throw new FormatException("address is below moduleBase; offset cannot be calculated");

                record = new RuntimeAddressRecord
                {
                    ReceivedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    GameTitle = GetString(data, "gameTitle"),
                    TitleId = GetString(data, "titleId"),
                    GameVersion = GetString(data, "gameVersion"),
                    ModuleName = module,
                    ProcessId = GetString(data, "processId"),
                    ModuleBase = "0x" + moduleBase.ToString("X"),
                    Address = "0x" + address.ToString("X"),
                    Offset = "0x" + (address - moduleBase).ToString("X"),
                    Label = GetString(data, "label")
                };
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : String.Empty;
        }

        private static bool TryAddress(string value, out ulong address)
        {
            address = 0;
            if (String.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt64.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
            return UInt64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            if (records.Count == 0) return;
            var dialog = new SaveFileDialog
            {
                FileName = "PLUTANA-DMP-runtime.json",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json"
            };
            if (dialog.ShowDialog(this) != true) return;

            var export = new
            {
                watermark = "PLUTANA-DMP",
                formatVersion = 1,
                readOnly = true,
                note = "Addresses are supplied by the connected PS4-side inspector. Offsets are calculated as address minus module base.",
                records = records
            };
            var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            File.WriteAllText(dialog.FileName, serializer.Serialize(export), new UTF8Encoding(false));
            statusText.Text = "Exported " + records.Count + " record(s).";
        }

        private void StopListener()
        {
            if (cancellation != null) cancellation.Cancel();
            if (listener != null) listener.Stop();
            listener = null;
            if (cancellation != null) cancellation.Dispose();
            cancellation = null;
            listenButton.Content = "Start listener";
            portTextBox.IsEnabled = true;
            statusText.Text = "Not listening";
        }

        protected override void OnClosed(EventArgs e)
        {
            StopListener();
            base.OnClosed(e);
        }
    }

    public sealed class RuntimeAddressRecord
    {
        public string ReceivedUtc { get; set; }
        public string GameTitle { get; set; }
        public string TitleId { get; set; }
        public string GameVersion { get; set; }
        public string ModuleName { get; set; }
        public string ProcessId { get; set; }
        public string ModuleBase { get; set; }
        public string Address { get; set; }
        public string Offset { get; set; }
        public string Label { get; set; }
        public string Game { get { return String.IsNullOrEmpty(TitleId) ? GameTitle : GameTitle + " (" + TitleId + ")"; } }
    }
}