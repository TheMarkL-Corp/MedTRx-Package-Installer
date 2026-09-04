using System;
using System.IO;
using System.Web.Script.Serialization;

namespace MedTRx
{
    public class AppConfig
    {
        public string url { get; set; }
        public string appName { get; set; }
        public bool startFullscreen { get; set; }
        public bool startMaximized { get; set; }
        public bool enableDevTools { get; set; }
        public bool enableNavigationKeys { get; set; }
        public double zoomFactor { get; set; }
        public bool allowExternalLinks { get; set; }
        public bool turboMode { get; set; }
        public bool autoOpenPdf { get; set; }
        public string pdfViewerMode { get; set; }

        public AppConfig()
        {
            url = "";
            appName = "MedTRx";
            startFullscreen = false;
            startMaximized = true;
            enableDevTools = false;
            enableNavigationKeys = true;
            zoomFactor = 1.0;
            allowExternalLinks = true;
            turboMode = true;
            autoOpenPdf = true;
            pdfViewerMode = "embedded";
        }
    }

    public static class ConfigManager
    {
        private static readonly string ExeDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "MedTRx"
        );

        public static string GetConfigFilePath()
        {
            // Primary: config.json next to executable
            string localConfig = Path.Combine(ExeDir, "config.json");
            if (File.Exists(localConfig))
            {
                return localConfig;
            }

            // Secondary: LocalAppData config
            string userConfig = Path.Combine(AppDataDir, "config.json");
            if (File.Exists(userConfig))
            {
                return userConfig;
            }

            // Default to local directory if writable, else AppData
            try
            {
                string testFile = Path.Combine(ExeDir, ".writetest");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return localConfig;
            }
            catch
            {
                return userConfig;
            }
        }

        public static AppConfig Load()
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var serializer = new JavaScriptSerializer();
                    var config = serializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText(
                        Path.Combine(AppDataDir, "error.log"), 
                        DateTime.Now.ToString("s") + " [Config Error] " + ex.Message + Environment.NewLine
                    );
                }
            }

            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            string configPath = GetConfigFilePath();
            string dir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(config);

            // Pretty format JSON for clean human readability
            json = json.Replace("{\"", "{\n  \"")
                       .Replace(",\"", ",\n  \"")
                       .Replace("}", "\n}");

            File.WriteAllText(configPath, json);
        }
    }
}
