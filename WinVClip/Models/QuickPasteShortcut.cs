using System.Collections.Generic;

namespace WinVClip.Models
{
    
    
    
    public enum QuickPasteMouseButton
    {
        Left,       
        Middle,     
        WheelUp,    
        WheelDown   
    }

    
    
    
    public enum QuickPasteAction
    {
        Split,               
        OpenInBrowser,       
        GenerateQRCode,      
        Edit,                
        Copy,                
        Delete,              
        PasteAsPlainText,    
        PasteRemoveNewlines, 
        PasteAndDelete,      
        Group,               
        CustomRegex          
    }

    
    
    
    public class QuickPasteShortcut
    {
        
        public int ClipboardType { get; set; } = -1;
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public QuickPasteMouseButton MouseButton { get; set; } = QuickPasteMouseButton.Middle;
        public QuickPasteAction Action { get; set; } = QuickPasteAction.PasteRemoveNewlines;

        
        public string QuickCommandName { get; set; } = "";

        
        public bool Enabled { get; set; } = true;

        
        public QuickPasteShortcut Clone()
        {
            return new QuickPasteShortcut
            {
                ClipboardType = ClipboardType,
                Ctrl = Ctrl,
                Shift = Shift,
                Alt = Alt,
                MouseButton = MouseButton,
                Action = Action,
                QuickCommandName = QuickCommandName ?? "",
                Enabled = Enabled
            };
        }

        
        public static List<QuickPasteShortcut> CreateDefaults() => new()
        {
            new() { Ctrl = true,  MouseButton = QuickPasteMouseButton.Left,   Action = QuickPasteAction.PasteAsPlainText },
            new() { Shift = true, MouseButton = QuickPasteMouseButton.Left,   Action = QuickPasteAction.PasteRemoveNewlines },
            new() {               MouseButton = QuickPasteMouseButton.Middle, Action = QuickPasteAction.PasteRemoveNewlines },
            new() { Alt = true,   MouseButton = QuickPasteMouseButton.Middle, Action = QuickPasteAction.OpenInBrowser },
        };
    }
}
