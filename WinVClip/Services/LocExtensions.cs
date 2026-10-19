using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WinVClip.Services
{
    /// <summary>本地化文本写入的目标属性；Auto 表示按控件类型推断。</summary>
    public enum LocProp
    {
        Auto,
        Text,
        Content,
        Header,
        ToolTip
    }

    /// <summary>表驱动本地化扩展：以 (控件, 键, 默认文本) 表批量应用本地化文本，语言切换时整表重放。</summary>
    public static class LocExtensions
    {
        /// <summary>按控件类型分发写入目标（TextBlock→Text、Button/CheckBox 等→Content、MenuItem/TabItem→Header）；未覆盖的类型忽略，保留可扩展。</summary>
        public static void LocalizeAll(this Window window, params (DependencyObject control, string key, string fallback)[] items)
        {
            if (items == null) return;
            foreach (var item in items)
                ApplyOne(item.control, item.key, item.fallback, LocProp.Auto);
        }

        /// <summary>同 LocalizeAll，但允许逐项显式指定目标属性（如 ToolTip）。</summary>
        public static void LocalizeAll(this Window window, params (DependencyObject control, string key, string fallback, LocProp prop)[] items)
        {
            if (items == null) return;
            foreach (var item in items)
                ApplyOne(item.control, item.key, item.fallback, item.prop);
        }

        private static void ApplyOne(DependencyObject control, string key, string fallback, LocProp prop)
        {
            if (control == null) return;
            string text = Loc.Get(key, fallback);
            switch (prop)
            {
                case LocProp.ToolTip:
                    if (control is FrameworkElement fe)
                        fe.ToolTip = text;
                    return;
                case LocProp.Text:
                    if (control is TextBlock tb) tb.Text = text;
                    else if (control is Run run) run.Text = text;
                    else if (control is TextBox box) box.Text = text;
                    return;
                case LocProp.Content:
                    if (control is ContentControl cc) cc.Content = text;
                    return;
                case LocProp.Header:
                    if (control is MenuItem mi) mi.Header = text;
                    else if (control is HeaderedContentControl hcc) hcc.Header = text;
                    return;
                default:
                    switch (control)
                    {
                        case TextBlock tbx: tbx.Text = text; break;
                        case Run runx: runx.Text = text; break;
                        case TextBox boxx: boxx.Text = text; break;
                        case MenuItem mix: mix.Header = text; break;
                        case HeaderedContentControl hccx: hccx.Header = text; break;
                        case ContentControl ccx: ccx.Content = text; break;
                    }
                    return;
            }
        }
    }
}
