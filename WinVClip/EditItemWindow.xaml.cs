using System;
using System.Windows;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class EditItemWindow : Window
    {
        private readonly ClipboardItem _item;

        public EditItemWindow(ClipboardItem item)
        {
            _item = item;
            DataContext = _item;
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            Title = Loc.Get("EditItem.Title", "编辑内容");
            TitleText.Text = Loc.Get("EditItem.EditClipboard", "编辑剪贴板内容");
            CancelButton.Content = Loc.Get("Common.Cancel", "取消");
            SaveButton.Content = Loc.Get("Common.Save", "保存");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 确保窗口在主窗口位置弹出
            if (Owner != null)
            {
                Left = Owner.Left;
                Top = Owner.Top;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var text = ContentTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("内容不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Content = text.Trim();
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