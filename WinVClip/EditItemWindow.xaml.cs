using System;
using System.Windows;
using WinVClip.Models;

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
            if (string.IsNullOrWhiteSpace(_item.Content))
            {
                MessageBox.Show("内容不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
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