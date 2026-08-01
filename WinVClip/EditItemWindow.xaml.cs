using System;
using System.Linq;
using System.Windows;
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
            LoadQuickCommands();
        }

        private void ApplyLocalization()
        {
            Title = Loc.Get("EditItem.Title", "编辑内容");
            TitleText.Text = Loc.Get("EditItem.EditClipboard", "编辑剪贴板内容");
            ActionLabel.Text = Loc.Get("EditItem.Action.Label", "快捷命令");
            ExecuteButton.Content = Loc.Get("EditItem.Action.Execute", "执行");
            ResetButton.Content = Loc.Get("EditItem.Action.Reset", "重置");
            CancelButton.Content = Loc.Get("Common.Cancel", "取消");
            SaveButton.Content = Loc.Get("Common.Save", "保存");
        }

        private void LoadQuickCommands()
        {
            var settings = App.SettingsService?.Settings;
            if (settings?.QuickCommands == null)
            {
                ActionComboBox.ItemsSource = null;
                return;
            }

            var itemType = (int)_item.Type;
            var applicable = settings.QuickCommands
                .Where(qc => qc != null && qc.Enabled && QuickCommand.TypeIncludes(qc.ClipboardType, itemType))
                .ToList();

            ActionComboBox.ItemsSource = applicable;
            if (applicable.Count > 0)
                ActionComboBox.SelectedIndex = 0;
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

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionComboBox.SelectedItem is not QuickCommand qc)
            {
                MessageBox.Show(
                    Loc.Get("EditItem.Action.NoCommand", "请先选择一个快捷命令"),
                    Loc.Get("Message.Info", "提示"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string processed = QuickCommand.ApplyRules(ContentTextBox.Text, qc.Rules);
                ContentTextBox.Text = processed;
            }
            catch (Exception ex)
            {
                var msgTemplate = Loc.Get("EditItem.Action.ExecuteFailed", "快捷命令执行失败：{0}");
                MessageBox.Show(
                    string.Format(msgTemplate, ex.Message),
                    Loc.Get("Message.Error", "错误"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show(
                    Loc.Get("EditItem.EmptyNotAllowed", "内容不能为空"),
                    Loc.Get("Message.Error", "错误"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Content = text.Trim();
            if (_wasRichText)
            {
                // 富文本编辑后保存，转换为文本类型并清空富文本相关字段
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
    }
}
