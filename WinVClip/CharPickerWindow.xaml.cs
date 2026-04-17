using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinVClip.Services;

namespace WinVClip
{
    public partial class CharPickerWindow : Window
    {
        private readonly string _sourceText;
        private readonly List<string> _charTokens = new();
        private readonly List<Border> _charElements = new();
        private readonly HashSet<int> _selectedIndices = new();
        private bool _isDragging;
        private int _dragStartIndex = -1;
        private int _lastDragIndex = -1;
        private bool _isUpdatingUI;
        private bool _dragSelectMode;
        private int _lastClickedIndex = -1;
        private const int BatchSize = 100;
        private int _renderedCount;

        public event EventHandler<string> OnInsert;

        public CharPickerWindow(string text)
        {
            InitializeComponent();
            _sourceText = text ?? "";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += (_, _) =>
            {
                ApplyLocalization();
                InitializeCharElements();
            };
            Unloaded += (_, _) => ThemeService.Instance.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, string theme)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var element in _charElements)
                {
                    var index = (int)element.DataContext;
                    UpdateElementStyle(element, _selectedIndices.Contains(index));
                }
            });
        }

        private void ApplyLocalization()
        {
            var title = Loc.Get("CharPicker.Title", "拆分选字");
            Title = title;
            SelectedTextLabel.Text = Loc.Get("CharPicker.SelectedTextLabel", "待插入文字:");
            InsertButton.Content = Loc.Get("CharPicker.Insert", "插入");
            SelectAllCheckBox.Content = Loc.Get("CharPicker.SelectAll", "全选");
            InvertButton.ToolTip = Loc.Get("CharPicker.InvertSelection", "反选");
            CopyButton.ToolTip = Loc.Get("CharPicker.Copy", "复制");
            SearchButton.ToolTip = Loc.Get("CharPicker.Search", "搜索");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Win32.SetWindowExStyle(hwnd, Win32.WS_EX_NOACTIVATE);
            ThemeService.Instance.ThemeChanged += OnThemeChanged;
        }

        private void InitializeCharElements()
        {
            _charTokens.Clear();
            _charElements.Clear();
            _selectedIndices.Clear();
            _renderedCount = 0;
            CharWrapPanel.Children.Clear();

            _charTokens.AddRange(SplitText(_sourceText));
            RenderBatch();
        }

        private void RenderBatch()
        {
            if (_renderedCount >= _charTokens.Count) return;

            var end = Math.Min(_renderedCount + BatchSize, _charTokens.Count);
            for (int i = _renderedCount; i < end; i++)
            {
                var element = CreateCharElement(_charTokens[i], i);
                _charElements.Add(element);
                CharWrapPanel.Children.Add(element);
            }
            _renderedCount = end;

            if (_renderedCount < _charTokens.Count)
            {
                Dispatcher.BeginInvoke(new Action(RenderBatch), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private static List<string> SplitText(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var normalizedText = Regex.Replace(text, @"[\s\u3000]+", " ");
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
                else if (IsAsciiLetter(c))
                {
                    int start = i;
                    while (i < normalizedText.Length && IsAsciiLetter(normalizedText[i])) i++;
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

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private Border CreateCharElement(string text, int index)
        {
            var border = new Border
            {
                DataContext = index,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            UpdateElementStyle(border, false);

            border.MouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Right)
                {
                    ShowCharContextMenu(text, index, e.GetPosition(this));
                    e.Handled = true;
                    return;
                }
                if (Keyboard.Modifiers == ModifierKeys.Shift && _lastClickedIndex >= 0)
                {
                    SelectRange(_lastClickedIndex, index);
                    _lastClickedIndex = index;
                }
                else
                {
                    _isDragging = true;
                    _dragStartIndex = index;
                    _dragSelectMode = !_selectedIndices.Contains(index);
                    SetSelection(index, _dragSelectMode);
                    _lastClickedIndex = index;
                }
                e.Handled = true;
            };
            border.MouseMove += (_, _) =>
            {
                if (!_isDragging || _dragStartIndex < 0) return;
                var hit = VisualTreeHelper.HitTest(CharWrapPanel, Mouse.GetPosition(CharWrapPanel));
                if (hit?.VisualHit != null && FindParent<Border>(hit.VisualHit) is { DataContext: int i } && i != _lastDragIndex)
                {
                    _lastDragIndex = i;
                    SetSelection(i, _dragSelectMode);
                }
            };
            border.MouseUp += (_, _) => { _isDragging = false; _dragStartIndex = -1; _lastDragIndex = -1; };

            return border;
        }

        private void UpdateElementStyle(Border element, bool isSelected)
        {
            if (isSelected)
            {
                element.Background = (Brush)FindResource("AccentColor");
                element.BorderBrush = (Brush)FindResource("AccentColor");
                element.BorderThickness = new Thickness(1);
                ((TextBlock)element.Child).Foreground = Brushes.White;
            }
            else
            {
                element.Background = (Brush)FindResource("ItemBackground");
                element.BorderBrush = (Brush)FindResource("BorderBrush");
                element.BorderThickness = new Thickness(1);
                ((TextBlock)element.Child).Foreground = (Brush)FindResource("TextForeground");
            }
        }

        private void ShowCharContextMenu(string text, int index, Point position)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("ContextMenuStyle") };

            var copyItem = new MenuItem { Header = Loc.Get("CharPicker.CopyChar", "复制该字") };
            copyItem.Click += (_, _) =>
            {
                try { Clipboard.SetText(text); } catch { }
            };
            menu.Items.Add(copyItem);

            var searchItem = new MenuItem { Header = Loc.Get("CharPicker.SearchChar", "搜索该字") };
            searchItem.Click += (_, _) =>
            {
                string url = App.SettingsService.GetSearchUrl(text);
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            menu.Items.Add(searchItem);

            menu.Items.Add(new Separator());

            var toggleItem = new MenuItem
            {
                Header = _selectedIndices.Contains(index)
                    ? Loc.Get("CharPicker.DeselectChar", "取消选择该字")
                    : Loc.Get("CharPicker.SelectChar", "选择该字")
            };
            toggleItem.Click += (_, _) =>
            {
                SetSelection(index, !_selectedIndices.Contains(index));
            };
            menu.Items.Add(toggleItem);

            menu.Placement = PlacementMode.AbsolutePoint;
            menu.HorizontalOffset = position.X;
            menu.VerticalOffset = position.Y;
            menu.IsOpen = true;
        }

        private void SetSelection(int index, bool select)
        {
            if (select)
                _selectedIndices.Add(index);
            else
                _selectedIndices.Remove(index);
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
            for (int i = 0; i < _charElements.Count; i++)
            {
                var isSelected = _selectedIndices.Contains(i);
                UpdateElementStyle(_charElements[i], isSelected);
            }
            SelectedTextBlock.Text = string.Join("", _selectedIndices.OrderBy(i => i).Select(i => _charTokens[i]));

            if (SelectAllCheckBox != null)
            {
                _isUpdatingUI = true;
                SelectAllCheckBox.IsChecked = _selectedIndices.Count == _charTokens.Count && _charTokens.Count > 0;
                _isUpdatingUI = false;
            }
            if (SearchButton != null)
                SearchButton.IsEnabled = _selectedIndices.Count > 0;
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

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            _selectedIndices.Clear();
            for (int i = 0; i < _charTokens.Count; i++)
                _selectedIndices.Add(i);
            UpdateUI();
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            _selectedIndices.Clear();
            UpdateUI();
        }

        private void InvertButton_Click(object sender, RoutedEventArgs e)
        {
            var newSelected = new HashSet<int>();
            for (int i = 0; i < _charTokens.Count; i++)
            {
                if (!_selectedIndices.Contains(i))
                    newSelected.Add(i);
            }
            _selectedIndices.Clear();
            foreach (var idx in newSelected)
                _selectedIndices.Add(idx);
            UpdateUI();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            DoCopy();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedTextBlock.Text))
                return;

            string searchText = SelectedTextBlock.Text.Trim();
            string url = App.SettingsService.GetSearchUrl(searchText);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void DoCopy()
        {
            if (string.IsNullOrEmpty(SelectedTextBlock.Text)) return;
            try
            {
                Clipboard.SetText(SelectedTextBlock.Text);
                ShowCopyFeedback();
            }
            catch { }
        }

        private async void ShowCopyFeedback()
        {
            var originalContent = CopyButton.Content;
            CopyButton.Content = "✓";
            await Task.Delay(800);
            CopyButton.Content = originalContent;
        }

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
