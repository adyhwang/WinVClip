using System.Collections.Generic;

namespace WinVClip.Models
{
    /// <summary>
    /// 全局快捷键配置：修饰键 + 普通按键 → 对剪贴板历史中第 N 项执行指定操作。
    /// 与 <see cref="QuickPasteShortcut"/>（主界面快捷键，修饰键+鼠标）的区别：
    /// 本类使用键盘按键而非鼠标，且作用于固定序号的剪贴板条目。
    /// </summary>
    public class GlobalHotkey
    {
        /// <summary>作用于剪贴板历史的第几项（1–10）。1 = 最顶（最新）一项。</summary>
        public int ItemIndex { get; set; } = 1;

        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }

        /// <summary>普通按键名称（如 V、F1、Enter）。留空表示未设置。</summary>
        public string Key { get; set; } = "";

        /// <summary>触发后执行的操作。CustomRegex 时使用 <see cref="QuickCommandName"/>。</summary>
        public QuickPasteAction Action { get; set; } = QuickPasteAction.PasteAsPlainText;

        /// <summary>仅当 Action == CustomRegex 时使用：对应快捷命令的 Name。</summary>
        public string QuickCommandName { get; set; } = "";

        /// <summary>是否启用此条目（持久化到 settings.json）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>把字段拼成 HotkeyService 可识别的标准化快捷键字符串（如 "Ctrl+Shift+V"）。</summary>
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

        /// <summary>深拷贝。</summary>
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

        /// <summary>程序首次运行时的默认全局快捷键（默认为空列表，由用户自行添加）。</summary>
        public static List<GlobalHotkey> CreateDefaults() => new();
    }
}
