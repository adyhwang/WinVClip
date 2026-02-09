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
        public string CustomSearchEngineUrl { get; set; } = "";

        [JsonIgnore]
        public List<SearchEngine> SearchEngines { get; set; } = new List<SearchEngine>();
    }
}
