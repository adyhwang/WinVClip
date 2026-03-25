using System;
using System.Collections.Generic;

namespace WinVClip.Models
{
    public class LanguageInfo
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    public class LanguageData
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Dictionary<string, string> Strings { get; set; } = new Dictionary<string, string>();
    }
}
