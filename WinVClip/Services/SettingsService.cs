using System;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace WinVClip.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private Models.AppSettings _settings = null!;
        private readonly object _lock = new object();

        public event Action? SettingsChanged;

        public SettingsService()
        {
            _settingsPath = GetSettingsPath();
            LoadSettings();
        }

        private string GetSettingsPath()
        {
            // 优先使用BaseDirectory
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            
            try
            {
                // 尝试写入测试文件，检查权限
                string testPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_permission.txt");
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                return basePath;
            }
            catch
            {
                // 权限不足，使用ApplicationData目录
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinVClip");
                Directory.CreateDirectory(appDataPath);
                return Path.Combine(appDataPath, "settings.json");
            }
        }

        public Models.AppSettings Settings
        {
            get
            {
                lock (_lock)
                {
                    return _settings;
                }
            }
            set
            {
                lock (_lock)
                {
                    _settings = value;
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<Models.AppSettings>(json) ?? new Models.AppSettings();
                }
                else
                {
                    // 根据设置文件路径确定数据库路径
                    string dbPath = GetDatabasePath();
                    _settings = new Models.AppSettings
                    {
                        Hotkey = "Ctrl+Shift+V",
                        DatabasePath = dbPath
                    };
                }
            }
            catch
            {
                // 根据设置文件路径确定数据库路径
                string dbPath = GetDatabasePath();
                _settings = new Models.AppSettings
                {
                    Hotkey = "Ctrl+Shift+V",
                    DatabasePath = dbPath
                };
            }
        }

        private string GetDatabasePath()
        {
            // 检查设置文件是否在ApplicationData目录
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (_settingsPath.StartsWith(appDataPath, StringComparison.OrdinalIgnoreCase))
            {
                // 设置文件在ApplicationData目录，数据库也放在同一目录
                string dbDir = Path.GetDirectoryName(_settingsPath) ?? appDataPath;
                return Path.Combine(dbDir, "clipboard_history.db");
            }
            else
            {
                // 设置文件在BaseDirectory，数据库也放在同一目录
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clipboard_history.db");
            }
        }

        public void SaveSettings()
        {
            try
            {
                lock (_lock)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    var json = JsonSerializer.Serialize(_settings, options);
                    File.WriteAllText(_settingsPath, json);
                }
                SettingsChanged?.Invoke();
            }
            catch
            {
            }
        }

        public void UpdateHotkey(string hotkey)
        {
            Settings.Hotkey = hotkey;
            SaveSettings();
        }

        public void UpdateMonitoring(bool enabled)
        {
            Settings.MonitorEnabled = enabled;
            SaveSettings();
        }

        public void UpdateCaptureSettings(bool images, bool files)
        {
            Settings.CaptureImages = images;
            Settings.CaptureFiles = files;
            SaveSettings();
        }

        public void UpdateDuplicateHandling(bool removeDuplicates)
        {
            Settings.RemoveDuplicates = removeDuplicates;
            SaveSettings();
        }

        public void UpdateCleanupSettings(bool enabled, int retentionDays)
        {
            Settings.EnableAutoCleanup = enabled;
            Settings.RetentionDays = retentionDays;
            SaveSettings();
        }

        /// <summary>
        /// 检查是否已设置为开机启动（从注册表读取）
        /// </summary>
        public bool IsStartupEnabled()
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null)
                {
                    var value = key.GetValue("WinVClip");
                    return value != null;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 设置或取消开机启动
        /// </summary>
        public void SetStartupRegistry(bool enable)
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinVClip.exe");
                        key.SetValue("WinVClip", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("WinVClip", false);
                    }
                }
            }
            catch
            {
            }
        }

        public void InitializeSearchEngines()
        {
            if (Settings.SearchEngines == null || Settings.SearchEngines.Count == 0)
            {
                Settings.SearchEngines = new System.Collections.Generic.List<Models.SearchEngine>
                {
                    new Models.SearchEngine { Id = "bingCN", Name = "BingCN", Url = "https://cn.bing.com/search?q=%s", IsCustom = false },
                    new Models.SearchEngine { Id = "bing", Name = "Bing", Url = "https://www.bing.com/search?q=%s", IsCustom = false },
                    new Models.SearchEngine { Id = "baidu", Name = "百度", Url = "https://www.baidu.com/s?wd=%s&ie=UTF-8", IsCustom = false },
                    new Models.SearchEngine { Id = "duckduckgo", Name = "DuckDuckGo", Url = "https://duckduckgo.com/?q=%s", IsCustom = false },
                    new Models.SearchEngine { Id = "google", Name = "Google", Url = "https://www.google.com/search?q=%s", IsCustom = false },
                    new Models.SearchEngine { Id = "so", Name = "360搜索", Url = "https://www.so.com/s?q=%s", IsCustom = false },
                    new Models.SearchEngine { Id = "custom", Name = "自定义", Url = "", IsCustom = true }
                };
                SaveSettings();
            }
        }

        public void UpdateSearchEngineSettings(string selectedId, string customUrl)
        {
            Settings.SelectedSearchEngineId = selectedId;
            Settings.CustomSearchEngineUrl = customUrl;
            SaveSettings();
        }

        public string GetSearchUrl(string text)
        {
            InitializeSearchEngines();
            
            var engine = Settings.SearchEngines.FirstOrDefault(e => e.Id == Settings.SelectedSearchEngineId);
            if (engine == null)
            {
                engine = Settings.SearchEngines.FirstOrDefault(e => e.Id == "google") ?? Settings.SearchEngines[0];
            }
            
            if (engine.IsCustom)
            {
                var url = Settings.CustomSearchEngineUrl;
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = "https://www.bing.com/search?q=%s";
                }
                return url.Replace("%s", Uri.EscapeDataString(text));
            }
            
            return engine.Url.Replace("%s", Uri.EscapeDataString(text));
        }
    }
}
