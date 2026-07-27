using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Globalization;

namespace WinVClip.Services
{
    public class LanguageData
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Dictionary<string, string> Strings { get; set; } = new Dictionary<string, string>();
    }

    public class LanguageInfo
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    public class LocalizationService
    {
        private static LocalizationService? _instance;
        private static readonly object _lock = new object();

        private readonly string _languageFolderPath;
        private LanguageData _currentLanguage = new LanguageData();
        private Dictionary<string, LanguageInfo> _availableLanguages = new Dictionary<string, LanguageInfo>();

        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LocalizationService();
                    }
                }
                return _instance;
            }
        }

        public event Action? LanguageChanged;

        public string CurrentLanguageCode => _currentLanguage.Code;
        public string CurrentLanguageDisplayName => _currentLanguage.DisplayName;
        public IReadOnlyDictionary<string, LanguageInfo> AvailableLanguages => _availableLanguages;

        private LocalizationService()
        {
            _languageFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language");
        }

        public void Initialize(string? preferredLanguageCode = null)
        {
            EnsureLanguageFolderExists();
            ScanAvailableLanguages();

            string languageToUse = DetermineLanguageToUse(preferredLanguageCode);
            LoadLanguage(languageToUse);
        }

        private void EnsureLanguageFolderExists()
        {
            bool needsCopy = false;

            if (!Directory.Exists(_languageFolderPath))
            {
                Directory.CreateDirectory(_languageFolderPath);
                needsCopy = true;
            }
            else
            {
                var existingFiles = Directory.GetFiles(_languageFolderPath, "*.json");
                if (existingFiles.Length == 0)
                {
                    needsCopy = true;
                }
                else
                {
                    foreach (var file in existingFiles)
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var data = JsonSerializer.Deserialize<LanguageData>(json);
                            if (data == null || data.Strings == null || data.Strings.Count == 0)
                            {
                                needsCopy = true;
                                break;
                            }
                        }
                        catch
                        {
                            needsCopy = true;
                            break;
                        }
                    }
                }
            }

            if (needsCopy)
            {
                CopyBuiltinLanguages();
            }
        }

        private void CopyBuiltinLanguages()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("WinVClip.Resources.Languages."))
                .ToList();

            foreach (var resourceName in resourceNames)
            {
                var fileName = resourceName.Replace("WinVClip.Resources.Languages.", "");
                var destPath = Path.Combine(_languageFolderPath, fileName);

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    File.WriteAllText(destPath, content);
                }
            }
        }

        private void ScanAvailableLanguages()
        {
            _availableLanguages.Clear();

            if (!Directory.Exists(_languageFolderPath))
                return;

            var files = Directory.GetFiles(_languageFolderPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<LanguageData>(json);
                    if (data != null && !string.IsNullOrEmpty(data.Code))
                    {
                        _availableLanguages[data.Code] = new LanguageInfo
                        {
                            Code = data.Code,
                            DisplayName = data.DisplayName,
                            FileName = Path.GetFileName(file)
                        };
                    }
                }
                catch
                {
                }
            }
        }

        private string DetermineLanguageToUse(string? preferredLanguageCode)
        {
            if (!string.IsNullOrEmpty(preferredLanguageCode) && _availableLanguages.ContainsKey(preferredLanguageCode))
            {
                return preferredLanguageCode;
            }

            string systemLanguage = GetSystemLanguage();
            if (_availableLanguages.ContainsKey(systemLanguage))
            {
                return systemLanguage;
            }

            if (_availableLanguages.ContainsKey("en"))
            {
                return "en";
            }

            return _availableLanguages.Keys.FirstOrDefault() ?? "en";
        }

        private string GetSystemLanguage()
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                string cultureName = culture.Name;

                if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    if (cultureName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                        cultureName.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                        cultureName.Equals("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                        cultureName.Equals("zh-SG", StringComparison.OrdinalIgnoreCase))
                    {
                        return "zh-TW";
                    }
                    return "zh-CN";
                }

                return "en";
            }
            catch
            {
                return "en";
            }
        }

        public bool LoadLanguage(string languageCode)
        {
            if (!_availableLanguages.TryGetValue(languageCode, out var languageInfo))
                return false;

            var filePath = Path.Combine(_languageFolderPath, languageInfo.FileName);
            if (!File.Exists(filePath))
                return false;

            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<LanguageData>(json);
                if (data != null)
                {
                    _currentLanguage = data;
                    LanguageChanged?.Invoke();
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (_currentLanguage.Strings.TryGetValue(key, out var value))
            {
                return value;
            }
            return string.IsNullOrEmpty(defaultValue) ? key : defaultValue;
        }

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key, key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        public void RefreshAvailableLanguages()
        {
            ScanAvailableLanguages();
        }
    }

    public static class Loc
    {
        public static string Get(string key, string defaultValue = "")
        {
            return LocalizationService.Instance.GetString(key, defaultValue);
        }

        public static string Get(string key, params object[] args)
        {
            return LocalizationService.Instance.GetString(key, args);
        }

        public static string CurrentLanguageCode => LocalizationService.Instance.CurrentLanguageCode;
        public static string CurrentLanguageDisplayName => LocalizationService.Instance.CurrentLanguageDisplayName;
    }
}
