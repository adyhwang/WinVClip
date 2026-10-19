using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace WinVClip.Controls
{
    /// <summary>
    /// 支持关键词高亮的 TextBlock：按 Keywords/Mode/Enabled 在文本中标记命中片段。
    /// Mode：Normal（空格分词，命中任一）/ Regex（正则）/ Wildcard（* ? 通配）。
    /// </summary>
    public class HighlightTextBlock : TextBlock
    {
        public static readonly DependencyProperty KeywordsProperty = DependencyProperty.RegisterAttached(
            "Keywords", typeof(string), typeof(HighlightTextBlock), new PropertyMetadata("", OnVisualParamChanged));

        public static string GetKeywords(DependencyObject obj) => (string)obj.GetValue(KeywordsProperty);
        public static void SetKeywords(DependencyObject obj, string value) => obj.SetValue(KeywordsProperty, value);

        public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
            "Mode", typeof(string), typeof(HighlightTextBlock), new PropertyMetadata("Normal", OnVisualParamChanged));

        public static string GetMode(DependencyObject obj) => (string)obj.GetValue(ModeProperty);
        public static void SetMode(DependencyObject obj, string value) => obj.SetValue(ModeProperty, value);

        public static readonly DependencyProperty IsHighlightEnabledProperty = DependencyProperty.RegisterAttached(
            "IsHighlightEnabled", typeof(bool), typeof(HighlightTextBlock), new PropertyMetadata(false, OnVisualParamChanged));

        public static bool GetIsHighlightEnabled(DependencyObject obj) => (bool)obj.GetValue(IsHighlightEnabledProperty);
        public static void SetIsHighlightEnabled(DependencyObject obj, bool value) => obj.SetValue(IsHighlightEnabledProperty, value);

        private bool _rebuilding;

        public HighlightTextBlock()
        {
            // XAML 解析时 attached property 先于模板绑定应用，构造后需在 loaded 时重建一次
            Loaded += (s, e) => RebuildInlines();
        }

        static HighlightTextBlock()
        {
            // 覆盖 Text 属性元数据：绑定推送新文本时重建高亮（TextBlock 无 OnTextChanged 可重写）
            TextProperty.OverrideMetadata(typeof(HighlightTextBlock),
                new FrameworkPropertyMetadata(string.Empty, OnTextPropertyChanged));
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HighlightTextBlock block)
                block.RebuildInlines();
        }

        private static void OnVisualParamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HighlightTextBlock block)
                block.RebuildInlines();
        }

        private void RebuildInlines()
        {
            if (_rebuilding)
                return;

            _rebuilding = true;
            try
            {
                var text = Text ?? string.Empty;
                Inlines.Clear();

                var enabled = GetIsHighlightEnabled(this);
                var keywords = GetKeywords(this)?.Trim() ?? "";
                var mode = GetMode(this) ?? "Normal";

                if (!enabled || keywords.Length == 0 || text.Length == 0)
                {
                    Inlines.Add(new Run(text));
                    return;
                }

                var ranges = mode switch
                {
                    "Regex" => GetRegexRanges(text, keywords),
                    "Wildcard" => GetRegexRanges(text, WildcardToRegexPattern(keywords)),
                    _ => GetKeywordRanges(text, keywords)
                };

                if (ranges.Count == 0)
                {
                    Inlines.Add(new Run(text));
                    return;
                }

                ranges.Sort((a, b) => a.Start.CompareTo(b.Start));

                int pos = 0;
                var accent = TryFindResource("AccentColor") as Brush ?? Brushes.DodgerBlue;
                foreach (var (start, length) in ranges)
                {
                    if (start > pos)
                        Inlines.Add(new Run(text.Substring(pos, start - pos)));
                    if (start + length > text.Length)
                        break;
                    Inlines.Add(new Run(text.Substring(start, length))
                    {
                        Foreground = accent,
                        FontWeight = FontWeights.SemiBold
                    });
                    pos = start + length;
                }
                if (pos < text.Length)
                    Inlines.Add(new Run(text.Substring(pos)));
            }
            finally
            {
                _rebuilding = false;
            }
        }

        /// <summary>空格分词（命中任一关键词即算命中，与默认搜索行为一致）</summary>
        private static List<(int Start, int Length)> GetKeywordRanges(string text, string keywords)
        {
            var words = keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ranges = new List<(int, int)>();
            foreach (var word in words)
                ranges.AddRange(FindAllOrdinalIgnoreCase(text, word));
            return ranges;
        }

        private static IEnumerable<(int Start, int Length)> FindAllOrdinalIgnoreCase(string text, string word)
        {
            if (word.Length == 0)
                yield break;
            int idx;
            int start = 0;
            while ((idx = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                yield return (idx, word.Length);
                start = idx + word.Length;
            }
        }

        private static List<(int Start, int Length)> GetRegexRanges(string text, string pattern)
        {
            var ranges = new List<(int, int)>();
            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                foreach (Match m in regex.Matches(text))
                {
                    if (m.Success && m.Length > 0)
                        ranges.Add((m.Index, m.Length));
                }
            }
            catch (ArgumentException)
            {
                // 无效正则：不高亮
            }
            catch (RegexMatchTimeoutException)
            {
                // 超时：不高亮
            }
            return ranges;
        }

        /// <summary>通配符转正则：* → .*、? → .，其余字符转义</summary>
        private static string WildcardToRegexPattern(string wildcard)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in wildcard)
            {
                switch (c)
                {
                    case '*': sb.Append(".*"); break;
                    case '?': sb.Append('.'); break;
                    default: sb.Append(Regex.Escape(c.ToString())); break;
                }
            }
            return sb.ToString();
        }
    }
}
