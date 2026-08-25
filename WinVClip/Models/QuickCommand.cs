using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

        
        public static List<QuickCommand> CreateDefaults()
        {
            var list = new List<QuickCommand>();

            var extractPhones = new QuickCommand
            {
                Name = "提取全部手机号",
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
            };
            list.Add(extractPhones);

            return list;
        }
    }
}
