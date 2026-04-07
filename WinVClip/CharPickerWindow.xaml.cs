using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinVClip.Services;

namespace WinVClip
{
    public partial class CharPickerWindow : Window
    {
        private readonly string _sourceText;
        private readonly List<Button> _charButtons = new();
        private readonly HashSet<int> _selectedIndices = new();
        private bool _isDragging;
        private int _dragStartIndex = -1;

        public event EventHandler<string> OnInsert;

        public CharPickerWindow(string text)
        {
            InitializeComponent();
            _sourceText = text ?? "";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += (_, _) =>
            {
                ApplyLocalization();
                InitializeCharButtons();
            };
        }

        private void ApplyLocalization()
        {
            var title = Loc.Get("CharPicker.Title", "拆分选字");
            Title = title;
            WindowTitleText.Text = title;
            SelectedTextLabel.Text = Loc.Get("CharPicker.SelectedTextLabel", "待插入文字:");
            InsertButton.Content = Loc.Get("CharPicker.Insert", "插入");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Win32.SetWindowExStyle(hwnd, Win32.WS_EX_NOACTIVATE);
        }

        private void InitializeCharButtons()
        {
            var tokens = SplitText(_sourceText);
            for (int i = 0; i < tokens.Count; i++)
            {
                var button = CreateCharButton(tokens[i], i);
                _charButtons.Add(button);
                CharWrapPanel.Children.Add(button);
            }
        }

        private static List<string> SplitText(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var normalizedText = Regex.Replace(text, @"[\s　]+", " ");
            int i = 0;
            while (i < normalizedText.Length)
            {
                char c = normalizedText[i];
                if (c == ' ')
                {
                    result.Add(" ");
                    i++;
                }
                else if (IsChineseChar(c))
                {
                    result.Add(c.ToString());
                    i++;
                }
                else if (char.IsLetter(c))
                {
                    int start = i;
                    while (i < normalizedText.Length && char.IsLetter(normalizedText[i])) i++;
                    result.Add(normalizedText.Substring(start, i - start));
                }
                else if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < normalizedText.Length && char.IsDigit(normalizedText[i])) i++;
                    result.Add(normalizedText.Substring(start, i - start));
                }
                else
                {
                    result.Add(c.ToString());
                    i++;
                }
            }
            return result;
        }

        private static bool IsChineseChar(char c) => c >= '\u4e00' && c <= '\u9fff' || c >= '\u3400' && c <= '\u4dbf';

        private Button CreateCharButton(string text, int index)
        {
            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("CharButtonStyle"),
                DataContext = index
            };
            button.PreviewMouseDown += (_, e) =>
            {
                _isDragging = true;
                _dragStartIndex = index;
                ToggleSelection(index);
                e.Handled = true;
            };
            button.PreviewMouseMove += (_, _) =>
            {
                if (!_isDragging || _dragStartIndex < 0) return;
                var hit = VisualTreeHelper.HitTest(CharWrapPanel, Mouse.GetPosition(CharWrapPanel));
                if (hit?.VisualHit != null && FindParent<Button>(hit.VisualHit) is { DataContext: int i })
                    SelectRange(_dragStartIndex, i);
            };
            button.PreviewMouseUp += (_, _) => { _isDragging = false; _dragStartIndex = -1; };
            return button;
        }

        private void ToggleSelection(int index)
        {
            if (!_selectedIndices.Add(index)) _selectedIndices.Remove(index);
            UpdateUI();
        }

        private void SelectRange(int start, int end)
        {
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                _selectedIndices.Add(i);
            UpdateUI();
        }

        private void UpdateUI()
        {
            for (int i = 0; i < _charButtons.Count; i++)
                _charButtons[i].Tag = _selectedIndices.Contains(i) ? "Selected" : null;
            SelectedTextBlock.Text = string.Join("", _selectedIndices.OrderBy(i => i).Select(i => _charButtons[i].Content));
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedTextBlock.Text))
                OnInsert?.Invoke(this, SelectedTextBlock.Text);
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private static class Win32
        {
            public const int WS_EX_NOACTIVATE = 0x08000000;
            public const int GWL_EXSTYLE = -20;
            [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
            [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
            public static void SetWindowExStyle(IntPtr hwnd, int style) => SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | style);
        }
    }
}
