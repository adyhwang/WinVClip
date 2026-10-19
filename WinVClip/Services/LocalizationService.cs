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
        private readonly Dictionary<string, Dictionary<string, string>> _builtinStringsCache = new Dictionary<string, Dictionary<string, string>>();

        // 写回语言文件时使用的序列化选项：非 ASCII 字符直接输出而非 \uXXXX 转义
        private static readonly JsonSerializerOptions LanguageFileWriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = AppJsonContext.Default
        };

        // 编码器会把补充平面字符（emoji）写成代理对转义，写回前还原为字面字符
        private static readonly System.Text.RegularExpressions.Regex SurrogatePairEscape =
            new System.Text.RegularExpressions.Regex(@"\\u([dD][89abAB][0-9a-fA-F]{2})\\u([dD][c-fC-F][0-9a-fA-F]{2})");

        private static string UnescapeSurrogatePairs(string json)
        {
            return SurrogatePairEscape.Replace(json, match =>
            {
                var high = (char)Convert.ToInt32(match.Groups[1].Value, 16);
                var low = (char)Convert.ToInt32(match.Groups[2].Value, 16);
                return char.ConvertFromUtf32(char.ConvertToUtf32(high, low));
            });
        }

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
            _languageFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources\\Languages");
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
            if (!Directory.Exists(_languageFolderPath))
            {
                Directory.CreateDirectory(_languageFolderPath);
            }

            CopyBuiltinLanguages();
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

                // 目标语言文件已存在则跳过，保留用户自定义
                if (File.Exists(destPath))
                {
                    continue;
                }

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
                    var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.LanguageData);
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
                var data = JsonSerializer.Deserialize(json, AppJsonContext.Default.LanguageData);
                if (data != null)
                {
                    BackfillMissingStrings(data, filePath, languageInfo.FileName);
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

        /// <summary>读取指定内置语言文件的字符串表（进程内缓存，避免每次加载语言都解析）；不内嵌的语言返回空表。</summary>
        private Dictionary<string, string> GetBuiltinStrings(string fileName)
        {
            if (_builtinStringsCache.TryGetValue(fileName, out var cached))
                return cached;

            var result = new Dictionary<string, string>();
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("WinVClip.Resources.Languages." + fileName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var data = JsonSerializer.Deserialize(reader.ReadToEnd(), AppJsonContext.Default.LanguageData);
                    if (data?.Strings != null)
                        result = data.Strings;
                }
            }
            catch
            {
            }

            _builtinStringsCache[fileName] = result;
            return result;
        }

        /// <summary>
        /// 补齐本地语言文件中缺失的语言键：同语种内置文件的值优先、内置英文文件兜底，并写回本地文件。
        /// </summary>
        private void BackfillMissingStrings(LanguageData local, string filePath, string localFileName)
        {
            var sameLanguage = GetBuiltinStrings(localFileName);
            var english = GetBuiltinStrings("en.json");

            // 键集合以英文内置文件为准，同语种内置文件的值覆盖之
            var reference = new Dictionary<string, string>();
            foreach (var pair in english)
                reference[pair.Key] = pair.Value;
            foreach (var pair in sameLanguage)
                reference[pair.Key] = pair.Value;

            if (reference.Count == 0)
                return;

            foreach (var pair in reference)
            {
                if (!local.Strings.ContainsKey(pair.Key))
                    local.Strings[pair.Key] = pair.Value;
            }

            // 与磁盘内容不一致才写回
            try
            {
                var merged = UnescapeSurrogatePairs(JsonSerializer.Serialize(local, LanguageFileWriteOptions));
                if (!string.Equals(File.ReadAllText(filePath), merged, StringComparison.Ordinal))
                    File.WriteAllText(filePath, merged);
            }
            catch
            {
            }
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

        /// <summary>
        /// 分组显示名：内置默认分组在数据库中固定为 "Favorite"，展示时按当前语言转换；
        /// 其余用户自建分组原样返回。
        /// </summary>
        public static string GetGroupName(string? name)
            => name == "Favorite" ? Get("GroupManage.DefaultGroup", "收藏") : name ?? "";
    }
}
