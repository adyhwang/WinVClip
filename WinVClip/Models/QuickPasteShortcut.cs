using System.Collections.Generic;

namespace WinVClip.Models
{
    /// <summary>
    /// 便捷粘贴快捷键的鼠标按键类型。
    /// </summary>
    public enum QuickPasteMouseButton
    {
        Left,       // 鼠标左键
        Middle,     // 鼠标中键
        WheelUp,    // 鼠标滚轮上
        WheelDown   // 鼠标滚轮下
    }

    /// <summary>
    /// 便捷粘贴快捷键触发后执行的操作类型。
    /// </summary>
    public enum QuickPasteAction
    {
        Split,               // 拆分选字
        OpenInBrowser,       // 在浏览器打开
        GenerateQRCode,      // 生成二维码
        Edit,                // 编辑
        Copy,                // 复制
        Delete,              // 删除
        PasteAsPlainText,    // 以纯文本粘贴
        PasteRemoveNewlines, // 移除换行符以纯文本粘贴
        Group,               // 分组（弹出分组菜单）
        CustomRegex          // 自定义处理文本（正则提取后粘贴）
    }

    /// <summary>
    /// 单条便捷粘贴快捷键配置。
    /// </summary>
    public class QuickPasteShortcut
    {
        /// <summary>-1 = 所有类型；否则为 ClipboardType 枚举值（0=Text,1=Image,2=FileList,3=RichText）</summary>
        public int ClipboardType { get; set; } = -1;
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public QuickPasteMouseButton MouseButton { get; set; } = QuickPasteMouseButton.Middle;
        public QuickPasteAction Action { get; set; } = QuickPasteAction.PasteRemoveNewlines;

        /// <summary>仅当 Action == CustomRegex 且从下拉框选择了「快捷命令」分组下的项时使用：对应快捷命令的 Name。</summary>
        public string QuickCommandName { get; set; } = "";

        /// <summary>是否启用此条目（持久化到 settings.json）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>深拷贝。</summary>
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

        /// <summary>程序首次运行时的默认便捷粘贴快捷键。</summary>
        public static List<QuickPasteShortcut> CreateDefaults() => new()
        {
            new() { Ctrl = true,  MouseButton = QuickPasteMouseButton.Left,   Action = QuickPasteAction.PasteAsPlainText },
            new() { Shift = true, MouseButton = QuickPasteMouseButton.Left,   Action = QuickPasteAction.PasteRemoveNewlines },
            new() {               MouseButton = QuickPasteMouseButton.Middle, Action = QuickPasteAction.PasteRemoveNewlines },
            new() { Alt = true,   MouseButton = QuickPasteMouseButton.Middle, Action = QuickPasteAction.OpenInBrowser },
        };
    }
}
