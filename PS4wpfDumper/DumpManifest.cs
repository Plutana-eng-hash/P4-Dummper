using System;
using System.IO;
using System.Xml;

namespace PS4wpfDumper
{
    /// <summary>
    /// Writes a sidecar provenance file without modifying any files in the game dump.
    /// </summary>
    internal static class DumpManifest
    {
        public static void Write(string dumpDirectory, string title, string titleId,
            string contentId, string version, string appVersion, string dumpType)
        {
            if (string.IsNullOrWhiteSpace(dumpDirectory))
                throw new ArgumentException("A dump directory is required.", "dumpDirectory");

            Directory.CreateDirectory(dumpDirectory);
            string manifestPath = Path.Combine(dumpDirectory, "PLUTANA-DMP.xml");

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new System.Text.UTF8Encoding(false)
            };

            using (var writer = XmlWriter.Create(manifestPath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("PlutanaDump");
                writer.WriteAttributeString("watermark", "Plutana-DMP");
                writer.WriteElementString("Tool", "Plutana-DMP");
                writer.WriteElementString("CreatedUtc", DateTime.UtcNow.ToString("o"));
                writer.WriteElementString("Title", title ?? string.Empty);
                writer.WriteElementString("TitleId", titleId ?? string.Empty);
                writer.WriteElementString("ContentId", contentId ?? string.Empty);
                writer.WriteElementString("Version", version ?? string.Empty);
                writer.WriteElementString("AppVersion", appVersion ?? string.Empty);
                writer.WriteElementString("DumpType", dumpType ?? string.Empty);
                writer.WriteElementString("Format", "Extracted PS4 game files");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            File.WriteAllText(Path.Combine(dumpDirectory, "PLUTANA-DMP.txt"),
                "PLUTANA-DMP\r\n" +
                "Created (UTC): " + DateTime.UtcNow.ToString("u") + "\r\n" +
                "Title: " + (title ?? string.Empty) + "\r\n" +
                "Title ID: " + (titleId ?? string.Empty) + "\r\n" +
                "Content ID: " + (contentId ?? string.Empty) + "\r\n" +
                "Version: " + (version ?? string.Empty) + "\r\n" +
                "Dump type: " + (dumpType ?? string.Empty) + "\r\n");
        }
    }
}
