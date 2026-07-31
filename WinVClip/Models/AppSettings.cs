using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinVClip.Models
{
    public class AppSettings
    {
        public string Hotkey { get; set; } = "Ctrl+Shift+V";
        public bool MonitorEnabled { get; set; } = true;
        public bool CaptureImages { get; set; } = true;
        public bool CaptureFiles { get; set; } = true;
        public bool RemoveDuplicates { get; set; } = true;
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
        public bool UseSystemCharPanel { get; set; } = true;
        public DateTime? LastVacuumDate { get; set; } = null;
        public string StartupLastExePath { get; set; } = "";

        /// <summary>主界面快捷键配置列表。首次运行自动填充默认配置。</summary>
        public List<QuickPasteShortcut> QuickPasteShortcuts { get; set; } = QuickPasteShortcut.CreateDefaults();

        /// <summary>全局快捷键配置列表（修饰键+键盘按键 → 对第 N 项剪贴板条目执行操作）。首次运行为空。</summary>
        public List<GlobalHotkey> GlobalHotkeys { get; set; } = GlobalHotkey.CreateDefaults();

        /// <summary>用户自定义快捷命令：正则匹配文本 → 替换（或提取）后粘贴。首次运行自动填充默认配置。</summary>
        public List<QuickCommand> QuickCommands { get; set; } = QuickCommand.CreateDefaults();

        [JsonIgnore]
        public List<SearchEngine> SearchEngines { get; set; } = new List<SearchEngine>();
    }
}
