using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WinVClip.Models
{
    public class CharDataRoot
    {
        [JsonPropertyName("groups")]
        public List<CharGroupData> Groups { get; set; } = new List<CharGroupData>();
    }

    public class CharGroupData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("nameZh")]
        public string NameZh { get; set; } = "";

        [JsonPropertyName("nameEn")]
        public string NameEn { get; set; } = "";

        [JsonPropertyName("chars")]
        public List<string> Chars { get; set; } = new List<string>();
    }
}
