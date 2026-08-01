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
}
