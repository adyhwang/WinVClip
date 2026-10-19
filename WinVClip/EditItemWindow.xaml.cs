using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class EditItemWindow : Window
    {
        private readonly ClipboardItem _item;
        private readonly string _originalContent;
        private readonly bool _wasRichText;

        public EditItemWindow(ClipboardItem item)
        {
            _item = item;
            _originalContent = item.Content ?? "";
            _wasRichText = item.Type == ClipboardType.RichText;
            DataContext = _item;
            InitializeComponent();
            ApplyLocalization();
            AddRule();
        }

        private void ApplyLocalization()
        {
            Title = Loc.Get("EditItem.Title", "编辑内容");
            TitleText.Text = Loc.Get("EditItem.EditClipboard", "编辑剪贴板内容");
            CustomCommandLabel.Text = Loc.Get("EditItem.CustomCommand", "自定义命令");
            AddRuleButton.Content = Loc.Get("Settings.QuickCommand.Button.AddRule", "＋ 添加规则");
            ImportCommandButton.ToolTip = Loc.Get("EditItem.Action.ImportRule", "从快捷命令导入规则");
            ExecuteButton.Content = Loc.Get("EditItem.Action.Execute", "执行");
            ResetButton.Content = Loc.Get("EditItem.Action.Reset", "重置");
            CancelButton.Content = Loc.Get("Common.Cancel", "取消");
            SaveButton.Content = Loc.Get("Common.Save", "保存");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (Owner != null)
            {
                Left = Owner.Left;
                Top = Owner.Top;
            }
        }

        private void AddRuleButton_Click(object sender, RoutedEventArgs e) => AddRule();

        /// <summary>追加一条默认规则行（字面查找替换）。</summary>
        private void AddRule()
        {
            RulesPanel.Children.Add(BuildRuleRow(new QuickCommandRule
            {
                Enabled = true,
                Function = QuickCommandFunction.StringReplace
            }));
        }

        private Grid BuildRuleRow(QuickCommandRule rule)
        {
            const double boxHeight = 36;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var enabledCb = new CheckBox
            {
                Name = "RuleEnabled",
                IsChecked = rule.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)FindResource("ModernCheckBoxStyle")
            };
            Grid.SetColumn(enabledCb, 0);
            row.Children.Add(enabledCb);

            var funcCb = new ComboBox
            {
                Name = "RuleFunc",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Height = boxHeight,
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            foreach (var fn in QuickCommand.OrderedFunctions)
            {
                var item = new ComboBoxItem { Tag = fn, Content = QuickCommand.FunctionDisplayName(fn) };
                funcCb.Items.Add(item);
                if (fn == rule.Function)
                    funcCb.SelectedItem = item;
            }
            funcCb.SelectionChanged += (s, e) =>
            {
                if (funcCb.SelectedItem is ComboBoxItem cbi && cbi.Tag is QuickCommandFunction fn)
                    ApplyParamVisibility(row, fn);
            };
            Grid.SetColumn(funcCb, 1);
            row.Children.Add(funcCb);

            var p1Tb = new TextBox
            {
                Name = "RuleP1",
                Text = rule.Param1 ?? "",
                Height = boxHeight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Style = (Style)FindResource("InputTextBoxStyle")
            };
            Grid.SetColumn(p1Tb, 2);
            row.Children.Add(p1Tb);

            var p2Tb = new TextBox
            {
                Name = "RuleP2",
                Text = rule.Param2 ?? "",
                Height = boxHeight,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("InputTextBoxStyle")
            };
            Grid.SetColumn(p2Tb, 3);
            row.Children.Add(p2Tb);

            var delBtn = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Style = (Style)FindResource("IconButtonStyle"),
                ToolTip = Loc.Get("Settings.QuickCommand.ToolTip.Delete", "删除规则")
            };
            delBtn.Click += (s, e) => RulesPanel.Children.Remove(row);
            Grid.SetColumn(delBtn, 4);
            row.Children.Add(delBtn);

            ApplyParamVisibility(row, rule.Function);
            return row;
        }

        private static void ApplyParamVisibility(Grid row, QuickCommandFunction fn)
        {
            int need = QuickCommand.FunctionParamCount(fn);
            foreach (var child in row.Children)
            {
                if (child is not TextBox tb) continue;
                int index = tb.Name == "RuleP1" ? 1 : tb.Name == "RuleP2" ? 2 : 0;
                if (index == 0) continue;
                bool show = need >= index;
                tb.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                tb.ToolTip = show ? QuickCommand.ParamToolTip(fn, index) : null;
                if (!show) tb.Text = "";
            }
        }

        private List<QuickCommandRule> CollectRules()
        {
            var rules = new List<QuickCommandRule>();
            foreach (var child in RulesPanel.Children)
            {
                if (child is not Grid row) continue;
                bool enabled = true;
                QuickCommandFunction func = QuickCommandFunction.StringReplace;
                string p1 = "", p2 = "";
                foreach (var element in row.Children)
                {
                    if (element is CheckBox cb && cb.Name == "RuleEnabled")
                        enabled = cb.IsChecked == true;
                    else if (element is ComboBox combo && combo.Name == "RuleFunc" &&
                             combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is QuickCommandFunction fn)
                        func = fn;
                    else if (element is TextBox tb1 && tb1.Name == "RuleP1")
                        p1 = tb1.Text ?? "";
                    else if (element is TextBox tb2 && tb2.Name == "RuleP2")
                        p2 = tb2.Text ?? "";
                }
                rules.Add(new QuickCommandRule { Enabled = enabled, Function = func, Param1 = p1, Param2 = p2 });
            }
            return rules;
        }

        private void LoadRules(IEnumerable<QuickCommandRule> rules)
        {
            RulesPanel.Children.Clear();
            foreach (var rule in rules)
                RulesPanel.Children.Add(BuildRuleRow(rule ?? new QuickCommandRule()));
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            var rules = CollectRules();
            if (rules.Count == 0)
            {
                AppMessageBox.Show(Loc.Get("EditItem.Action.NoRule", "请先添加规则"),
                    Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (!r.Enabled || QuickCommand.FunctionParamCount(r.Function) < 1 || !string.IsNullOrEmpty(r.Param1))
                    continue;
                string msg = string.Format(
                    Loc.Get("Settings.QuickCommand.Error.Param1Required", "{0} 的参数一（{1}）不能为空。"),
                    string.Format(Loc.Get("Settings.QuickCommand.Error.RuleN", "规则 #{0} [{1}]"),
                        i + 1, QuickCommand.FunctionDisplayName(r.Function)),
                    QuickCommand.ParamToolTip(r.Function, 1));
                AppMessageBox.Show(msg, Loc.Get("Common.Error", "错误"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ContentTextBox.Text = QuickCommand.ApplyRules(ContentTextBox.Text, rules);
            }
            catch (Exception ex)
            {
                var msgTemplate = Loc.Get("EditItem.Action.ExecuteFailed", "快捷命令执行失败：{0}");
                AppMessageBox.Show(
                    string.Format(msgTemplate, ex.Message),
                    Loc.Get("Message.Error", "错误"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            var menu = new ContextMenu
            {
                Style = TryFindResource("ContextMenuStyle") as Style,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = btn
            };

            int itemType = (int)_item.Type;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<QuickCommand>();

            var userCommands = App.SettingsService?.Settings?.QuickCommands;
            if (userCommands != null)
            {
                foreach (var qc in userCommands)
                {
                    if (qc != null && qc.Enabled && QuickCommand.TypeIncludes(qc.ClipboardType, itemType)
                        && seen.Add(CommandKey(qc)))
                        candidates.Add(qc);
                }
            }
            foreach (var tpl in QuickCommand.CreateTemplates())
            {
                if (QuickCommand.TypeIncludes(tpl.ClipboardType, itemType) && seen.Add(CommandKey(tpl)))
                    candidates.Add(tpl);
            }

            if (candidates.Count == 0)
            {
                menu.Items.Add(new MenuItem
                {
                    Header = Loc.Get("EditItem.Action.NoCommand", "请先选择一个快捷命令"),
                    IsEnabled = false,
                    Style = TryFindResource("MenuItemStyle") as Style
                });
            }
            foreach (var cmd in candidates)
            {
                var captured = cmd;
                var item = new MenuItem
                {
                    Header = captured.Name,
                    Style = TryFindResource("MenuItemStyle") as Style
                };
                item.Click += (s, args) => LoadRules(captured.Rules);
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        /// <summary>命令去重键：名称与规则内容都相同视为同一条。</summary>
        private static string CommandKey(QuickCommand qc)
        {
            var rules = qc.Rules ?? new List<QuickCommandRule>();
            return (qc.Name ?? "") + "\u0001" + string.Join("\u0002",
                rules.Select(r => (int)(r?.Function ?? 0) + "\u0003" + (r?.Param1 ?? "") + "\u0004" + (r?.Param2 ?? "")));
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ContentTextBox.Text = _originalContent;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var text = ContentTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                AppMessageBox.Show(
                    Loc.Get("EditItem.EmptyNotAllowed", "内容不能为空"),
                    Loc.Get("Message.Error", "错误"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Content = text.Trim();
            if (_wasRichText)
            {
                _item.Type = ClipboardType.Text;
                _item.RichContent = null;
                _item.RichFormat = null;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 双向绑定会把输入实时写回条目，取消或直接关闭时撤销，避免内存中的条目被未保存的修改污染
            if (DialogResult != true)
                _item.Content = _originalContent;
            base.OnClosed(e);
        }
    }
}
