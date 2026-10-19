using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinVClip.Models
{
    public class AppSettings
    {
        public string Hotkey { get; set; } = "Ctrl+Shift+V";
        public bool MonitorEnabled { get; set; } = true;
        // 捕获文本与富文本内容
        public bool CaptureText { get; set; } = true;
        public bool CaptureImages { get; set; } = true;
        public bool CaptureFiles { get; set; } = true;
        public bool RemoveDuplicates { get; set; } = false;

        // 忽略窗口：前台窗口的标题或进程名包含任一关键词时，不记录其剪贴板内容
        public List<string> IgnoredWindowKeywords { get; set; } = new List<string>();

        public string DatabasePath { get; set; } = "";
        public bool EnableAutoCleanup { get; set; } = false;
        public int RetentionDays { get; set; } = 30;
        public int MaxHistoryItems { get; set; } = 200;
        public string Theme { get; set; } = "Auto";
        public string SelectedSearchEngineId { get; set; } = "bing";
        public string CustomSearchEngineUrl { get; set; } = "https://www.bing.com/search?q=%s";
        public bool MoveToTopAfterPaste { get; set; } = false;
        public string PasteShortcutMode { get; set; } = "Auto";
        public bool IsAdministratorRun { get; set; } = false;
        public string Language { get; set; } = "";
        public int FontSize { get; set; } = 14;
        public bool ShowHotkeyTipOnStartup { get; set; } = true;
        public bool UseSystemCharPanel { get; set; } = false;
        public DateTime? LastVacuumDate { get; set; } = null;
        public string StartupLastExePath { get; set; } = "";

        // 主窗口尺寸（DIP）；0 表示未保存过，沿用 MainWindow.xaml 中的默认尺寸
        public double MainWindowWidth { get; set; } = 0;
        public double MainWindowHeight { get; set; } = 0;

        // 顺序粘贴：后进先出（LIFO）热键，默认 Ctrl+Shift+Z
        public string SequentialPasteLifoHotkey { get; set; } = "Ctrl+Shift+Z";

        // 顺序粘贴：先进先出（FIFO）热键，默认 Ctrl+Shift+X
        public string SequentialPasteFifoHotkey { get; set; } = "Ctrl+Shift+X";

        // 顺序粘贴：判定同一批记录/同一批按键的间隔秒数
        public int SequentialPasteIntervalSeconds { get; set; } = 8;

        // 搜索选项：关键词高亮（列表项中命中片段着色）
        public bool SearchHighlightEnabled { get; set; } = true;

        // 搜索选项：匹配全部关键词（空格分词全部命中才显示；关闭时命中任一关键词即显示）
        public bool SearchExactModeEnabled { get; set; } = true;

        // 搜索选项：搜索历史（搜索栏下方浮层显示最近搜索词）
        public bool SearchHistoryEnabled { get; set; } = true;

        // 搜索选项：拼音搜索（中文记录按全拼/拼音首字母匹配）
        public bool SearchPinyinEnabled { get; set; } = true;

        // 搜索选项：使用正则表达式（与通配符互斥）
        public bool SearchRegexEnabled { get; set; } = false;

        // 搜索选项：使用通配符（* 任意串、? 单字符；与正则互斥）
        public bool SearchWildcardEnabled { get; set; } = false;

        // 搜索历史（最近 10 条，配合 SearchHistoryEnabled 使用）
        public List<string> SearchHistory { get; set; } = new List<string>();

        public List<QuickPasteShortcut> QuickPasteShortcuts { get; set; } = QuickPasteShortcut.CreateDefaults();

        public List<GlobalHotkey> GlobalHotkeys { get; set; } = GlobalHotkey.CreateDefaults();

        public List<QuickCommand> QuickCommands { get; set; } = QuickCommand.CreateDefaults();

        [JsonIgnore]
        public List<SearchEngine> SearchEngines { get; set; } = new List<SearchEngine>();

        /// <summary>JSON 往返深拷贝，含全部可序列化字段与嵌套列表，新增字段自动纳入快照。</summary>
        public AppSettings Clone()
            => System.Text.Json.JsonSerializer.Deserialize(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this, WinVClip.Services.AppJsonContext.Default.AppSettings),
                WinVClip.Services.AppJsonContext.Default.AppSettings)!;
    }
}
