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
        public bool RemoveDuplicates { get; set; } = false;
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

        // 顺序粘贴：后进先出（LIFO）热键，默认 Ctrl+Shift+Z（Ctrl+Win+V 常被微信输入法等占用，Alt 组合易被软件内快捷键占用）
        public string SequentialPasteLifoHotkey { get; set; } = "Ctrl+Shift+Z";

        // 顺序粘贴：先进先出（FIFO）热键，默认 Ctrl+Shift+X
        public string SequentialPasteFifoHotkey { get; set; } = "Ctrl+Shift+X";

        // 顺序粘贴：判定同一批记录/同一批按键的间隔秒数
        public int SequentialPasteIntervalSeconds { get; set; } = 8;

        
        public List<QuickPasteShortcut> QuickPasteShortcuts { get; set; } = QuickPasteShortcut.CreateDefaults();

        
        public List<GlobalHotkey> GlobalHotkeys { get; set; } = GlobalHotkey.CreateDefaults();

        
        public List<QuickCommand> QuickCommands { get; set; } = QuickCommand.CreateDefaults();

        [JsonIgnore]
        public List<SearchEngine> SearchEngines { get; set; } = new List<SearchEngine>();
    }
}
