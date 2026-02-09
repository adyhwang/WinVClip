using System.Collections.Generic;

namespace WinVClip.Models
{
    public class SearchEngine
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public bool IsCustom { get; set; } = false;
    }

    public static class SearchEngineDefaults
    {
        public static readonly List<SearchEngine> Defaults = new List<SearchEngine>
        {
            new SearchEngine { Id = "bingCN", Name = "BingCN", Url = "https://cn.bing.com/search?q=%s", IsCustom = false },
            new SearchEngine { Id = "bing", Name = "Bing", Url = "https://www.bing.com/search?q=%s", IsCustom = false },
            new SearchEngine { Id = "baidu", Name = "百度", Url = "https://www.baidu.com/s?wd=%s&ie=UTF-8", IsCustom = false },
            new SearchEngine { Id = "duckduckgo", Name = "DuckDuckGo", Url = "https://duckduckgo.com/?q=%s", IsCustom = false },
            new SearchEngine { Id = "google", Name = "Google", Url = "https://www.google.com/search?q=%s", IsCustom = false },
            new SearchEngine { Id = "so", Name = "360搜索", Url = "https://www.so.com/s?q=%s", IsCustom = false },
            new SearchEngine { Id = "custom", Name = "自定义", Url = "", IsCustom = true }
        };
    }
}
