namespace WinVClip.Models
{
    /// <summary>搜索行为选项（由设置项派生，随查询传入 DatabaseService）</summary>
    public class SearchOptions
    {
        /// <summary>精确搜索：空格分词全部命中才显示；false 时命中任一关键词即显示</summary>
        public bool ExactMode { get; set; }

        /// <summary>使用正则表达式（与 Wildcard 互斥）</summary>
        public bool UseRegex { get; set; }

        /// <summary>使用通配符（* 任意串、? 单字符；与 UseRegex 互斥）</summary>
        public bool UseWildcard { get; set; }

        /// <summary>拼音搜索：同时匹配中文全拼/拼音首字母列</summary>
        public bool UsePinyin { get; set; }
    }
}
