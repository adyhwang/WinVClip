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
        private StartupTaskService _startupTask;

        public event Action? SettingsChanged;

        public StartupTaskService StartupTask
        {
            get
            {
                if (_startupTask == null)
                    _startupTask = new StartupTaskService(this);
                return _startupTask;
            }
        }

        public SettingsService()
        {
            _settingsPath = GetSettingsPath();
            LoadSettings();
        }

        private string GetSettingsPath()
        {
            
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            
            try
            {
                
                string testPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_permission.txt");
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                return basePath;
            }
            catch
            {
                
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
            
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (_settingsPath.StartsWith(appDataPath, StringComparison.OrdinalIgnoreCase))
            {
                
                string dbDir = Path.GetDirectoryName(_settingsPath) ?? appDataPath;
                return Path.Combine(dbDir, "clipboard_history.db");
            }
            else
            {
                
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

        public bool IsStartupEnabled()
        {
            return StartupTask.IsStartupEnabled();
        }

        public void SetStartupRegistry(bool enable)
        {
            if (enable)
                StartupTask.EnableStartup();
            else
                StartupTask.DisableStartup();
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

        public Services.PasteShortcutMode GetPasteShortcutMode()
        {
            return Settings.PasteShortcutMode?.ToLower() switch
            {
                "ctrlv" or "ctrl_v" or "ctrl-v" => Services.PasteShortcutMode.CtrlV,
                "shiftinsert" or "shift_insert" or "shift-insert" => Services.PasteShortcutMode.ShiftInsert,
                _ => Services.PasteShortcutMode.Auto
            };
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
