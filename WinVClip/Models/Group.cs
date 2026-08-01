using System;

namespace WinVClip.Models
{
    public class Group
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = "⭐";
        public DateTime CreatedAt { get; set; }
    }
}