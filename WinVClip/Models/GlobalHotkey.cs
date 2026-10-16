using System.Collections.Generic;

namespace WinVClip.Models
{
    
    
    
    
    
    public class GlobalHotkey
    {
        
        public int ItemIndex { get; set; } = 1;

        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }

        
        public string Key { get; set; } = "";

        
        public QuickPasteAction Action { get; set; } = QuickPasteAction.PasteAsPlainText;

        
        public string QuickCommandName { get; set; } = "";

        
        public bool Enabled { get; set; } = true;

        
        public string ToHotkeyString()
        {
            var parts = new List<string>(5);
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            if (!string.IsNullOrWhiteSpace(Key)) parts.Add(Key.Trim());
            return string.Join("+", parts);
        }

        
        public GlobalHotkey Clone()
        {
            return new GlobalHotkey
            {
                ItemIndex = ItemIndex,
                Ctrl = Ctrl,
                Shift = Shift,
                Alt = Alt,
                Win = Win,
                Key = Key ?? "",
                Action = Action,
                QuickCommandName = QuickCommandName ?? "",
                Enabled = Enabled
            };
        }

        
        public static List<GlobalHotkey> CreateDefaults() => new();
    }
}
