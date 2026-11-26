using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentFTP;

namespace PS4wpfDumper
{
    public sealed class GameMountItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public FtpListItem Item { get; private set; }
        public string DisplayName { get; private set; }
        public string Detail { get; private set; }
        public string TitleId { get; private set; }
        public string MountType { get; private set; }
        public string MountPath { get { return Item == null ? string.Empty : Item.FullName; } }
        public ImageSource Icon { get; private set; }

        public GameMountItem(FtpListItem item)
        {
            Item = item;
            string name = item == null ? string.Empty : item.Name;
            var match = System.Text.RegularExpressions.Regex.Match(name, @"[A-Z]{4}[0-9]{5}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            TitleId = match.Success ? match.Value.ToUpperInvariant() : name;
            MountType = name.EndsWith("-patch0", StringComparison.OrdinalIgnoreCase) ? "UPDATE" : "BASE";
            DisplayName = TitleId;
            Detail = name;
        }

        public void SetMetadata(string title, string titleId, string iconPath)
        {
            if (!string.IsNullOrWhiteSpace(title)) DisplayName = title.Trim();
            if (!string.IsNullOrWhiteSpace(titleId)) TitleId = titleId.Trim();
            Detail = TitleId + "  •  " + Item.Name;
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(Path.GetFullPath(iconPath), UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Icon = bitmap;
                }
                catch { Icon = null; }
            }
            RaiseChanged("DisplayName");
            RaiseChanged("TitleId");
            RaiseChanged("Detail");
            RaiseChanged("Icon");
        }

        private void RaiseChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}