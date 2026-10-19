using System.Text;

namespace WinVClip.Services
{
    /// <summary>
    /// 拼音索引生成：为文本内容生成 "全拼 首字母" 索引串（小写、空格分隔），
    /// 供拼音搜索时 LIKE 匹配。例："微信收藏" → "weixinshoucang wxsc"
    /// </summary>
    public static class PinyinService
    {
        public static string? BuildPinyinIndex(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var full = new StringBuilder(content.Length);
            var initials = new StringBuilder(content.Length / 2 + 1);
            var hasChinese = false;

            foreach (var c in content)
            {
                if (IsChineseChar(c))
                {
                    var pinyin = global::TinyPinyin.PinyinHelper.GetPinyin(c);
                    if (string.IsNullOrEmpty(pinyin))
                    {
                        full.Append(c);
                        initials.Append(c);
                        continue;
                    }
                    hasChinese = true;
                    full.Append(pinyin.ToLowerInvariant());
                    initials.Append(char.ToLowerInvariant(pinyin[0]));
                }
                else
                {
                    // 非中文（含英文/数字）原样保留，英文单词可直接被拼音索引命中
                    full.Append(char.ToLowerInvariant(c));
                    initials.Append(char.ToLowerInvariant(c));
                }
            }

            if (!hasChinese)
                return null; // 纯非中文内容无需拼音索引（Content 本身即可匹配）

            return $"{full} {initials}";
        }

        private static bool IsChineseChar(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF)   // CJK 基本区
                || (c >= 0x3400 && c <= 0x4DBF)   // CJK 扩展 A
                || (c >= 0xF900 && c <= 0xFAFF);  // CJK 兼容
        }
    }
}
