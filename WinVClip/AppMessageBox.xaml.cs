using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinVClip.Services;

namespace WinVClip
{
    public partial class AppMessageBox : Window
    {
        private MessageBoxResult _result;
        private MessageBoxResult _enterResult = MessageBoxResult.None;

        private static readonly object _openLock = new object();
        private static readonly List<IntPtr> _openDialogs = new List<IntPtr>();

        public AppMessageBox(string message, string caption, MessageBoxButton button, MessageBoxImage image, MessageBoxResult defaultResult)
        {
            InitializeComponent();

            TitleText.Text = string.IsNullOrEmpty(caption) ? " " : caption;
            MessageText.Text = message;
            _result = defaultResult == MessageBoxResult.None
                       ? (button == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel)
                       : defaultResult;

            SetupIcon(image);
            SetupButtons(button, defaultResult);
        }

        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage image)
        {
            return Show(message, caption, button, image, MessageBoxResult.None);
        }

        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button)
        {
            return Show(message, caption, button, MessageBoxImage.None, MessageBoxResult.None);
        }

        public static MessageBoxResult Show(string message, string caption)
        {
            return Show(message, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(message, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
        }

        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage image, MessageBoxResult defaultResult)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return (MessageBoxResult)dispatcher.Invoke(new Func<MessageBoxResult>(
                    () => Show(message, caption, button, image, defaultResult)));
            }

            var dialog = new AppMessageBox(message, caption, button, image, defaultResult);
            dialog.Owner = FindActiveWindow();
            dialog.WindowStartupLocation = dialog.Owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;

            var hwnd = GetHandle(dialog);
            lock (_openLock)
            {
                _openDialogs.Add(hwnd);
            }

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                lock (_openLock)
                {
                    _openDialogs.Remove(hwnd);
                }
            }

            return dialog._result;
        }

        private static IntPtr GetHandle(Window w)
        {
            return new WindowInteropHelper(w).EnsureHandle();
        }

        private static Window FindActiveWindow()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w.IsActive)
                    return w;
            }
            foreach (Window w in Application.Current.Windows)
            {
                if (w != Application.Current.MainWindow && w.IsVisible)
                    return w;
            }
            return Application.Current.MainWindow;
        }

        public static bool IsPointInDialog(System.Drawing.Point screenPoint)
        {
            lock (_openLock)
            {
                foreach (var hwnd in _openDialogs)
                {
                    RECT rect;
                    if (GetWindowRect(hwnd, out rect))
                    {
                        if (screenPoint.X >= rect.left && screenPoint.X < rect.right &&
                            screenPoint.Y >= rect.top && screenPoint.Y < rect.bottom)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void SetupIcon(MessageBoxImage image)
        {
            if (image == MessageBoxImage.Information || image == MessageBoxImage.Asterisk)
            {
                IconText.Text = "ℹ";
                IconText.Foreground = FindBrush("#FF0078D4");
                IconText.Visibility = Visibility.Visible;
            }
            else if (image == MessageBoxImage.Warning || image == MessageBoxImage.Exclamation)
            {
                IconText.Text = "⚠";
                IconText.Foreground = FindBrush("#FFE6A23C");
                IconText.Visibility = Visibility.Visible;
            }
            else if (image == MessageBoxImage.Error || image == MessageBoxImage.Stop || image == MessageBoxImage.Hand)
            {
                IconText.Text = "✕";
                IconText.Foreground = FindBrush("#FFE5484D");
                IconText.Visibility = Visibility.Visible;
            }
            else if (image == MessageBoxImage.Question)
            {
                IconText.Text = "?";
                IconText.Foreground = FindBrush("#FF0078D4");
                IconText.Visibility = Visibility.Visible;
            }
            else
            {
                IconText.Visibility = Visibility.Collapsed;
            }
        }

        private System.Windows.Media.SolidColorBrush FindBrush(string hex)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new System.Windows.Media.SolidColorBrush(color);
        }

        private void SetupButtons(MessageBoxButton button, MessageBoxResult defaultResult)
        {
            ButtonPanel.Children.Clear();

            var ok = new Button { Content = Loc.Get("Common.OK", "确定") };
            var cancel = new Button { Content = Loc.Get("Common.Cancel", "取消") };
            var yes = new Button { Content = Loc.Get("Common.Yes", "是") };
            var no = new Button { Content = Loc.Get("Common.No", "否") };

            MessageBoxResult primary = MessageBoxResult.None;

            switch (button)
            {
                case MessageBoxButton.OK:
                    ButtonPanel.Children.Add(ok);
                    primary = MessageBoxResult.OK;
                    _enterResult = MessageBoxResult.OK;
                    break;
                case MessageBoxButton.OKCancel:
                    ButtonPanel.Children.Add(cancel);
                    ButtonPanel.Children.Add(ok);
                    primary = MessageBoxResult.OK;
                    _enterResult = MessageBoxResult.OK;
                    break;
                case MessageBoxButton.YesNoCancel:
                    ButtonPanel.Children.Add(cancel);
                    ButtonPanel.Children.Add(no);
                    ButtonPanel.Children.Add(yes);
                    primary = MessageBoxResult.Yes;
                    _enterResult = MessageBoxResult.Yes;
                    break;
                default:
                    ButtonPanel.Children.Add(no);
                    ButtonPanel.Children.Add(yes);
                    primary = MessageBoxResult.Yes;
                    _enterResult = MessageBoxResult.Yes;
                    break;
            }

            ApplyButtonStyle(ok, MessageBoxResult.OK, primary);
            ApplyButtonStyle(cancel, MessageBoxResult.Cancel, primary);
            ApplyButtonStyle(yes, MessageBoxResult.Yes, primary);
            ApplyButtonStyle(no, MessageBoxResult.No, primary);

            ok.Click += (s, e) => Finish(MessageBoxResult.OK);
            cancel.Click += (s, e) => Finish(MessageBoxResult.Cancel);
            yes.Click += (s, e) => Finish(MessageBoxResult.Yes);
            no.Click += (s, e) => Finish(MessageBoxResult.No);
        }

        private void ApplyButtonStyle(Button btn, MessageBoxResult result, MessageBoxResult primary)
        {
            btn.Style = (Style)FindResource(result == primary ? "DialogPrimaryButtonStyle" : "DialogButtonStyle");
        }

        private void Finish(MessageBoxResult result)
        {
            _result = result;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _result = MessageBoxResult.Cancel;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _enterResult != MessageBoxResult.None)
            {
                _result = _enterResult;
                Close();
                e.Handled = true;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}