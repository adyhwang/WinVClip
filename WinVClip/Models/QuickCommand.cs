using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinVClip.Models
{
    /// <summary>快捷命令规则可调用的文本处理函数。</summary>
    public enum QuickCommandFunction
    {
        /// <summary>字面文本查找替换：Param1=查找，Param2=替换。</summary>
        StringReplace,
        /// <summary>正则查找替换：Param1=pattern，Param2=替换模板（可含$1等组引用）。</summary>
        RegexReplace,
        /// <summary>正则提取匹配内容：Param1=pattern。输出所有匹配项 Value 直接拼接。</summary>
        RegexMatches,
        /// <summary>正则转义：对文本中的正则特殊字符加反斜杠，无参数。</summary>
        RegexEscape,
        /// <summary>去除首尾空白字符（string.Trim），无参数。</summary>
        StringTrim,
        /// <summary>转为大写（InvariantCulture），无参数。</summary>
        StringToUpper,
        /// <summary>转为小写（InvariantCulture），无参数。</summary>
        StringToLower,
        /// <summary>正则分割：Param1=pattern。按分隔符切片后用换行重新拼接输出。</summary>
        RegexSplit,
        /// <summary>按行去重：移除重复的行（保留首次出现顺序），无参数。</summary>
        DistinctRemoveDuplicate,
        /// <summary>移除所有空白字符（空格、Tab、换行、不间断空格等），无参数。</summary>
        RemoveWhiteSpace,
        /// <summary>移除换行符（\r 和 \n），其他字符保持不变，无参数。</summary>
        RemoveNewLines
    }

    /// <summary>
    /// 快捷命令中的单条规则。每条规则对应一个 <see cref="QuickCommandFunction"/> 及其实参。
    /// 一个 <see cref="QuickCommand"/> 按顺序依次执行所有 <see cref="Enabled"/>=true 的规则。
    /// </summary>
    public class QuickCommandRule
    {
        /// <summary>是否启用该规则（false 时跳过执行）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>要调用的文本处理函数。</summary>
        public QuickCommandFunction Function { get; set; } = QuickCommandFunction.StringReplace;

        /// <summary>参数一：查找串 / Pattern（视 <see cref="Function"/> 而定）。</summary>
        public string Param1 { get; set; } = "";

        /// <summary>参数二：替换串 / 替换模板（视 <see cref="Function"/> 而定）。</summary>
        public string Param2 { get; set; } = "";

        public QuickCommandRule Clone()
        {
            return new QuickCommandRule
            {
                Enabled = Enabled,
                Function = Function,
                Param1 = Param1 ?? "",
                Param2 = Param2 ?? "",
            };
        }
    }

    /// <summary>
    /// 用户自定义的"文本快捷命令"：包含若干顺序执行的规则链。
    /// 对剪贴板文本依次执行启用的规则，结果设置回剪贴板并粘贴。
    /// </summary>
    public class QuickCommand
    {
        /// <summary>快捷命令名称（必填，菜单显示名）。</summary>
        public string Name { get; set; } = "";

        /// <summary>规则链：按列表顺序依次执行所有 <see cref="QuickCommandRule.Enabled"/>=true 的条目。</summary>
        public List<QuickCommandRule> Rules { get; set; } = new List<QuickCommandRule>();

        /// <summary>是否启用此条目（持久化到 settings.json）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>适用的剪贴板类型。-1=所有类型；-2=文本+富文本；0=文本；1=图片；2=文件；3=富文本。</summary>
        public int ClipboardType { get; set; } = -1;

        #region 类型判断辅助

        /// <summary>
        /// 判断配置的类型（可能是组合值，如 -2 = 文本+富文本）是否包含某个具体的 item 类型（0/1/2/3）。
        /// </summary>
        public static bool TypeIncludes(int configuredType, int actualItemType)
        {
            if (configuredType == -1) return true;
            if (configuredType == actualItemType) return true;
            if (configuredType == -2)
                return actualItemType == 0 || actualItemType == 3;
            return false;
        }

        /// <summary>
        /// 判断两种配置类型（都可能是组合值）是否存在公共的具体 item 类型（0/1/2/3 之一）。
        /// </summary>
        public static bool TypesIntersect(int typeA, int typeB)
        {
            if (typeA == -1 || typeB == -1) return true;
            for (int actual = 0; actual <= 3; actual++)
            {
                if (TypeIncludes(typeA, actual) && TypeIncludes(typeB, actual))
                    return true;
            }
            return false;
        }

        #endregion

        public QuickCommand Clone()
        {
            var cloned = new QuickCommand
            {
                Name = Name ?? "",
                Enabled = Enabled,
                ClipboardType = ClipboardType,
            };
            cloned.Rules = Rules?.Select(r => r.Clone()).ToList() ?? new List<QuickCommandRule>();
            return cloned;
        }

        /// <summary>
        /// 对一段文本按顺序执行启用的规则链（克隆自 MainWindow.ApplyQuickCommandRule 的逻辑，
        /// 以便 EditItemWindow 等其他窗口也能复用）。
        /// </summary>
        public static string ApplyRules(string input, IEnumerable<QuickCommandRule> rules, bool firstOnly = false)
        {
            if (input == null) input = "";
            if (rules == null) return input;

            string current = input;
            foreach (var rawRule in rules)
            {
                if (rawRule == null || !rawRule.Enabled) continue;
                current = ApplyRule(current, rawRule.Function, rawRule.Param1 ?? "", rawRule.Param2 ?? "", firstOnly);
            }
            return current;
        }

        /// <summary>
        /// 单条规则执行函数。
        /// </summary>
        public static string ApplyRule(string input, QuickCommandFunction func, string param1, string param2, bool firstOnly)
        {
            if (input == null) input = "";
            switch (func)
            {
                case QuickCommandFunction.StringReplace:
                    if (string.IsNullOrEmpty(param1)) return input;
                    if (firstOnly)
                    {
                        int idx = input.IndexOf(param1, StringComparison.Ordinal);
                        if (idx < 0) return input;
                        return input.Substring(0, idx) + (param2 ?? "") +
                               input.Substring(idx + param1.Length);
                    }
                    return input.Replace(param1, param2 ?? "");

                case QuickCommandFunction.RegexReplace:
                    if (string.IsNullOrEmpty(param1)) return input;
                    var rr = new Regex(param1);
                    if (firstOnly) return rr.Replace(input, param2 ?? "", 1);
                    return rr.Replace(input, param2 ?? "");

                case QuickCommandFunction.RegexMatches:
                    if (string.IsNullOrEmpty(param1)) return "";
                    var rm2 = new Regex(param1);
                    var mc2 = rm2.Matches(input);
                    if (mc2.Count == 0) return "";
                    if (string.IsNullOrEmpty(param2))
                    {
                        var sb2 = new System.Text.StringBuilder();
                        foreach (Match m in mc2)
                            sb2.Append(m.Value);
                        return sb2.ToString();
                    }
                    var list = new List<string>(mc2.Count);
                    foreach (Match m2 in mc2)
                        list.Add(m2.Value);
                    return string.Join(param2, list);

                case QuickCommandFunction.RegexEscape:
                    return Regex.Escape(input);

                case QuickCommandFunction.StringTrim:
                    return input.Trim();

                case QuickCommandFunction.StringToUpper:
                    return input.ToUpperInvariant();

                case QuickCommandFunction.StringToLower:
                    return input.ToLowerInvariant();

                case QuickCommandFunction.RegexSplit:
                    if (string.IsNullOrEmpty(param1)) return input;
                    var parts = Regex.Split(input, param1);
                    return string.Join(Environment.NewLine, parts);

                case QuickCommandFunction.DistinctRemoveDuplicate:
                    string newLine = "\n";
                    if (input.Contains("\r\n")) newLine = "\r\n";
                    else if (input.Contains("\r")) newLine = "\r";
                    string[] lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    var seen = new HashSet<string>();
                    var unique = new List<string>();
                    foreach (var line in lines)
                    {
                        if (seen.Add(line)) unique.Add(line);
                    }
                    return string.Join(newLine, unique);

                case QuickCommandFunction.RemoveWhiteSpace:
                    var nosp = new System.Text.StringBuilder(input.Length);
                    foreach (char c in input)
                    {
                        if (!char.IsWhiteSpace(c)) nosp.Append(c);
                    }
                    return nosp.ToString();

                case QuickCommandFunction.RemoveNewLines:
                    return input.Replace("\r", "").Replace("\n", "");

                default:
                    return input;
            }
        }

        /// <summary>首次运行或点击全局重置时填充的默认「文本快捷命令」。</summary>
        public static List<QuickCommand> CreateDefaults()
        {
            var list = new List<QuickCommand>();

            var extractPhones = new QuickCommand
            {
                Name = "提取全部手机号",
                ClipboardType = -2, // 仅文本 + 富文本
                Rules = new List<QuickCommandRule>
                {
                    new QuickCommandRule
                    {
                        Enabled = true,
                        Function = QuickCommandFunction.RegexMatches,
                        Param1 = @"1[3-9](?:[\s\-]?\d){9}",
                        Param2 = ","
                    },
                    new QuickCommandRule
                    {
                        Enabled = true,
                        Function = QuickCommandFunction.RemoveWhiteSpace,
                        Param1 = "",
                        Param2 = ""
                    },
                    new QuickCommandRule
                    {
                        Enabled = true,
                        Function = QuickCommandFunction.StringReplace,
                        Param1 = "-",
                        Param2 = ""
                    },
                    new QuickCommandRule
                    {
                        Enabled = true,
                        Function = QuickCommandFunction.RegexReplace,
                        Param1 = @"(?<=\d{11})(?=1[3-9]\d{9}(?!\d))",
                        Param2 = ","
                    }
                }
            };
            list.Add(extractPhones);

            return list;
        }
    }
}
