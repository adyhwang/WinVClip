using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WinVClip.Services;

namespace WinVClip.Models
{

    public enum QuickCommandFunction
    {

        StringReplace,

        RegexReplace,

        RegexMatches,

        RegexEscape,

        StringTrim,

        StringToUpper,

        StringToLower,

        RegexSplit,

        DistinctRemoveDuplicate,

        RemoveWhiteSpace,

        RemoveNewLines
    }

    public class QuickCommandRule
    {

        public bool Enabled { get; set; } = true;

        public QuickCommandFunction Function { get; set; } = QuickCommandFunction.StringReplace;

        public string Param1 { get; set; } = "";

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

    public class QuickCommand
    {

        public string Name { get; set; } = "";

        public List<QuickCommandRule> Rules { get; set; } = new List<QuickCommandRule>();

        public bool Enabled { get; set; } = true;

        public int ClipboardType { get; set; } = -1;

        #region 类型判断辅助

        public static bool TypeIncludes(int configuredType, int actualItemType)
        {
            if (configuredType == -1) return true;
            if (configuredType == actualItemType) return true;
            if (configuredType == -2)
                return actualItemType == 0 || actualItemType == 3;
            return false;
        }

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

        /// <summary>规则函数下拉的展示顺序。</summary>
        public static readonly QuickCommandFunction[] OrderedFunctions = new[]
        {
            QuickCommandFunction.StringReplace,
            QuickCommandFunction.RegexReplace,
            QuickCommandFunction.RegexMatches,
            QuickCommandFunction.RegexEscape,
            QuickCommandFunction.StringTrim,
            QuickCommandFunction.StringToUpper,
            QuickCommandFunction.StringToLower,
            QuickCommandFunction.RegexSplit,
            QuickCommandFunction.DistinctRemoveDuplicate,
            QuickCommandFunction.RemoveWhiteSpace,
            QuickCommandFunction.RemoveNewLines
        };

        public static string FunctionDisplayName(QuickCommandFunction f)
        {
            switch (f)
            {
                case QuickCommandFunction.StringReplace:          return Loc.Get("Settings.QuickCommand.Function.StringReplace",          "字面查找替换");
                case QuickCommandFunction.RegexReplace:           return Loc.Get("Settings.QuickCommand.Function.RegexReplace",           "正则查找替换");
                case QuickCommandFunction.RegexMatches:           return Loc.Get("Settings.QuickCommand.Function.RegexMatches",           "正则提取匹配");
                case QuickCommandFunction.RegexEscape:            return Loc.Get("Settings.QuickCommand.Function.RegexEscape",            "正则转义文本");
                case QuickCommandFunction.StringTrim:             return Loc.Get("Settings.QuickCommand.Function.StringTrim",             "去除首尾空白");
                case QuickCommandFunction.StringToUpper:          return Loc.Get("Settings.QuickCommand.Function.StringToUpper",          "转大写");
                case QuickCommandFunction.StringToLower:          return Loc.Get("Settings.QuickCommand.Function.StringToLower",          "转小写");
                case QuickCommandFunction.RegexSplit:             return Loc.Get("Settings.QuickCommand.Function.RegexSplit",             "正则分割(换行拼接)");
                case QuickCommandFunction.DistinctRemoveDuplicate:return Loc.Get("Settings.QuickCommand.Function.DistinctRemoveDuplicate","按行去重");
                case QuickCommandFunction.RemoveWhiteSpace:       return Loc.Get("Settings.QuickCommand.Function.RemoveWhiteSpace",       "移除全部空白");
                case QuickCommandFunction.RemoveNewLines:         return Loc.Get("Settings.QuickCommand.Function.RemoveNewLines",         "移除换行");
            }
            return f.ToString();
        }

        /// <summary>函数需要的参数个数（0~2）。</summary>
        public static int FunctionParamCount(QuickCommandFunction f)
        {
            switch (f)
            {
                case QuickCommandFunction.StringReplace:
                case QuickCommandFunction.RegexReplace:
                case QuickCommandFunction.RegexMatches:
                    return 2;
                case QuickCommandFunction.RegexSplit:
                    return 1;
                default:
                    return 0;
            }
        }

        public static string ParamToolTip(QuickCommandFunction f, int paramIndex)
        {
            if (paramIndex == 1)
            {
                switch (f)
                {
                    case QuickCommandFunction.StringReplace: return Loc.Get("Settings.QuickCommand.Param.Find", "查找串");
                    case QuickCommandFunction.RegexReplace:
                    case QuickCommandFunction.RegexMatches:
                    case QuickCommandFunction.RegexSplit:
                        return Loc.Get("Settings.QuickCommand.Param.Pattern", "正则 Pattern");
                }
            }
            else if (paramIndex == 2)
            {
                switch (f)
                {
                    case QuickCommandFunction.StringReplace: return Loc.Get("Settings.QuickCommand.Param.Replace", "替换串");
                    case QuickCommandFunction.RegexReplace:  return Loc.Get("Settings.QuickCommand.Param.ReplTemplate", "替换模板(支持$1组引用)");
                    case QuickCommandFunction.RegexMatches:  return Loc.Get("Settings.QuickCommand.Param.Separator", "多个结果分隔符(留空=直接拼接)");
                }
            }
            return "";
        }

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

        /// <summary>内置快捷命令模板（名称按当前语言本地化）。</summary>
        public static List<QuickCommand> CreateTemplates()
        {
            return new List<QuickCommand>
            {
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Phone", "提取全部手机号"),
                    ClipboardType = -2,
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
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.IdCard", "提取全部身份证号"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\b[1-9]\d{5}(?:19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[0-9Xx]\b",
                            Param2 = ","
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.StringReplace,
                            Param1 = " ",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Email", "提取全部邮箱地址"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\b[\w\.\-]+@[\w\-]+(?:\.[\w\-]+)+\b",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Url", "提取全部网址链接"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = DatabaseService.LinkMatchPattern,
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.NumberSplit", "提取全部数字（逗号分割）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\d+",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.NumberConcat", "提取全部数字（不分割）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\d+",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Chinese", "提取全部中文汉字"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"[\u4e00-\u9fa5]",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.CollapseSpaces", "清除多余空格"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"[\t]+",
                            Param2 = " "
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"\s{2,}",
                            Param2 = " "
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"^\s+|\s+$",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.DistinctLines", "列表去重（换行数据）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"(?m)^\s+|\s+$",
                            Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"(?ms)^(.*?)(?:\r?\n\1)+(?=\r?\n|$)",
                            Param2 = "$1"
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.LinesToComma", "多行转逗号一行"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"\r?\n",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.CommaToLines", "逗号转多行（半角/全角逗号）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"[,，]",
                            Param2 = "\r\n"
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.StripHtml", "去除HTML标签"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"<[^>]+>",
                            Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.StringReplace,
                            Param1 = "&nbsp;",
                            Param2 = " "
                        }
                    }
                }
            };
        }

        /// <summary>新用户默认快捷命令：取内置模板第一条。</summary>
        public static List<QuickCommand> CreateDefaults()
        {
            return new List<QuickCommand> { CreateTemplates()[0].Clone() };
        }
    }
}
