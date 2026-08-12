using System;
using System.Collections.Generic;
using System.Linq;
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
        private bool _isPinned;

        public event EventHandler<string> OnInsert;

        public CharPickerWindow(string text)
        {
            InitializeComponent();
            _sourceText = text ?? "";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += (_, _) =>
            {
                ApplyLocalization();
                ApplyFontSize();
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

        private void ApplyFontSize()
        {
            var settingsService = App.SettingsService;
            if (settingsService != null)
            {
                var fontSize = settingsService.Settings.FontSize;
                var scale = fontSize / 14.0;
                ContentContainer.LayoutTransform = new ScaleTransform(scale, scale);
            }
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
            PinButton.ToolTip = Loc.Get("CharPicker.Pin", "置顶");
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

            var elements = new List<string>();
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
                elements.Add(enumerator.GetTextElement());

            int i = 0;
            while (i < elements.Count)
            {
                string elem = elements[i];

                if (IsLineBreak(elem))
                {
                    result.Add("↵");
                    i++;
                }
                else if (IsWhitespace(elem))
                {
                    result.Add(" ");
                    while (i + 1 < elements.Count && IsWhitespace(elements[i + 1]) && !IsLineBreak(elements[i + 1]))
                        i++;
                    i++;
                }
                else if (IsEmojiBase(elem))
                {
                    result.Add(CollectEmojiSequence(elements, ref i));
                }
                else if (IsCJKChar(elem))
                {
                    result.Add(elem);
                    i++;
                }
                else if (IsJapaneseKana(elem))
                {
                    result.Add(elem);
                    i++;
                }
                else if (IsKoreanSyllable(elem))
                {
                    result.Add(elem);
                    i++;
                }
                else if (IsLetter(elem))
                {
                    var words = SplitPascalCase(elements, ref i);
                    result.AddRange(words);
                }
                else if (IsDigit(elem))
                {
                    var start = i;
                    while (i < elements.Count && IsDigit(elements[i])) i++;
                    result.Add(string.Join("", elements.Skip(start).Take(i - start)));
                }
                else
                {
                    result.Add(elem);
                    i++;
                }
            }
            return result;
        }

        private static List<string> SplitPascalCase(List<string> elements, ref int index)
        {
            var result = new List<string>();
            var currentWord = new List<string>();

            while (index < elements.Count && IsLetter(elements[index]))
            {
                string elem = elements[index];
                bool isUpper = IsUpperCase(elem);
                bool nextIsLower = index + 1 < elements.Count && IsLetter(elements[index + 1]) && IsLowerCase(elements[index + 1]);

                if (currentWord.Count > 0 && isUpper && nextIsLower)
                {
                    result.Add(string.Join("", currentWord));
                    currentWord.Clear();
                }
                else if (currentWord.Count > 0 && isUpper && !nextIsLower && HasLowerCase(currentWord))
                {
                    result.Add(string.Join("", currentWord));
                    currentWord.Clear();
                }

                currentWord.Add(elem);
                index++;
            }

            if (currentWord.Count > 0)
                result.Add(string.Join("", currentWord));

            return result;
        }

        private static bool HasLowerCase(List<string> chars)
        {
            return chars.Any(c => IsLowerCase(c));
        }

        private static bool IsUpperCase(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return (c >= 'A' && c <= 'Z') || (c >= 'Ａ' && c <= 'Ｚ');
        }

        private static bool IsLowerCase(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return (c >= 'a' && c <= 'z') || (c >= 'ａ' && c <= 'ｚ');
        }

        private static bool IsLineBreak(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return c == '\n' || c == '\r' || c == '\u2028' || c == '\u2029';
        }

        private static bool IsWhitespace(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return char.IsWhiteSpace(c) || c == '\u3000';
        }

        private static bool IsCJKChar(string s)
        {
            if (s.Length == 1)
            {
                uint c = s[0];
                return (c >= 0x4E00 && c <= 0x9FFF)
                    || (c >= 0x3400 && c <= 0x4DBF)
                    || (c >= 0xF900 && c <= 0xFAFF)
                    || (c >= 0x2E80 && c <= 0x2EFF)
                    || (c >= 0x2F00 && c <= 0x2FDF)
                    || (c >= 0x3000 && c <= 0x303F)
                    || (c >= 0x31C0 && c <= 0x31EF)
                    || (c >= 0x3200 && c <= 0x32FF)
                    || (c >= 0x3300 && c <= 0x33FF)
                    || (c >= 0xFE30 && c <= 0xFE4F)
                    || (c >= 0xFF00 && c <= 0xFFEF);
            }
            if (s.Length == 2 && char.IsHighSurrogate(s[0]))
            {
                uint codePoint = (uint)char.ConvertToUtf32(s[0], s[1]);
                return (codePoint >= 0x20000 && codePoint <= 0x2A6DF)
                    || (codePoint >= 0x2A700 && codePoint <= 0x2B73F)
                    || (codePoint >= 0x2B740 && codePoint <= 0x2B81F)
                    || (codePoint >= 0x2B820 && codePoint <= 0x2CEAF)
                    || (codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF)
                    || (codePoint >= 0x2F800 && codePoint <= 0x2FA1F)
                    || (codePoint >= 0x30000 && codePoint <= 0x3134F);
            }
            return false;
        }

        private static bool IsJapaneseKana(string s)
        {
            if (s.Length != 1) return false;
            uint c = s[0];
            return (c >= 0x3040 && c <= 0x309F)
                || (c >= 0x30A0 && c <= 0x30FF)
                || (c >= 0x31F0 && c <= 0x31FF)
                || (c >= 0xFF65 && c <= 0xFF9F);
        }

        private static bool IsKoreanSyllable(string s)
        {
            if (s.Length != 1) return false;
            uint c = s[0];
            return (c >= 0xAC00 && c <= 0xD7AF)
                || (c >= 0x1100 && c <= 0x11FF)
                || (c >= 0x3130 && c <= 0x318F);
        }

        private static bool IsLetter(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= 'ａ' && c <= 'ｚ')
                || (c >= 'Ａ' && c <= 'Ｚ');
        }

        private static bool IsDigit(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return (c >= '0' && c <= '9')
                || (c >= '０' && c <= '９');
        }

        private static bool IsEmojiBase(string s)
        {
            if (s.Length == 1)
            {
                uint c = s[0];
                return (c >= 0x1F600 && c <= 0x1F64F)
                    || (c >= 0x1F300 && c <= 0x1F5FF)
                    || (c >= 0x1F680 && c <= 0x1F6FF)
                    || (c >= 0x1F1E6 && c <= 0x1F1FF)
                    || (c >= 0x1F900 && c <= 0x1F9FF)
                    || (c >= 0x1FA00 && c <= 0x1FA6F)
                    || (c >= 0x1FA70 && c <= 0x1FAFF)
                    || (c >= 0x2600 && c <= 0x26FF)
                    || (c >= 0x2700 && c <= 0x27BF)
                    || (c >= 0x2300 && c <= 0x23FF)
                    || (c >= 0x2B50 && c <= 0x2B55)
                    || (c >= 0x203C && c <= 0x3299)
                    || c == 0x00A9 || c == 0x00AE
                    || c == 0x2122 || c == 0x3030
                    || c == 0x303D || c == 0x3297
                    || c == 0xFE0F;
            }
            if (s.Length == 2 && char.IsHighSurrogate(s[0]))
            {
                uint cp = (uint)char.ConvertToUtf32(s[0], s[1]);
                return (cp >= 0x1F000 && cp <= 0x1F02F)
                    || (cp >= 0x1F0A0 && cp <= 0x1F0FF)
                    || (cp >= 0x1F100 && cp <= 0x1F1FF)
                    || (cp >= 0x1F600 && cp <= 0x1F64F)
                    || (cp >= 0x1F300 && cp <= 0x1F5FF)
                    || (cp >= 0x1F680 && cp <= 0x1F6FF)
                    || (cp >= 0x1F900 && cp <= 0x1F9FF)
                    || (cp >= 0x1FA00 && cp <= 0x1FA6F)
                    || (cp >= 0x1FA70 && cp <= 0x1FAFF)
                    || (cp >= 0x1FC00 && cp <= 0x1FCFF)
                    || (cp >= 0x27000 && cp <= 0x27BFF);
            }
            return false;
        }

        private static bool IsEmojiComponent(string s)
        {
            if (s.Length == 1)
            {
                uint c = s[0];
                return c == 0x200D  // ZWJ
                    || c == 0xFE0F  // VS16
                    || c == 0xFE0E  // VS15
                    || c == 0x20E3  // combining enclosing keycap
                    || (c >= 0x1F3FB && c <= 0x1F3FF)  // skin tone
                    || (c >= 0xE0020 && c <= 0xE007F);  // tag
            }
            if (s.Length == 2 && char.IsHighSurrogate(s[0]))
            {
                uint cp = (uint)char.ConvertToUtf32(s[0], s[1]);
                return (cp >= 0x1F3FB && cp <= 0x1F3FF);
            }
            return false;
        }

        private static bool IsRegionalIndicator(string s)
        {
            if (s.Length != 1) return false;
            return s[0] >= 0x1F1E6 && s[0] <= 0x1F1FF;
        }

        private static string CollectEmojiSequence(List<string> elements, ref int index)
        {
            var sequence = new List<string> { elements[index] };
            index++;

            // Flag sequence: Regional Indicator pair
            if (IsRegionalIndicator(sequence[0]) && index < elements.Count && IsRegionalIndicator(elements[index]))
            {
                sequence.Add(elements[index]);
                index++;
                return string.Join("", sequence);
            }

            // ZWJ sequence, modifier sequence, keycap sequence
            while (index < elements.Count)
            {
                string next = elements[index];

                if (next == "\u200D") // ZWJ
                {
                    sequence.Add(next);
                    index++;
                    if (index < elements.Count && (IsEmojiBase(elements[index]) || IsEmojiComponent(elements[index])))
                    {
                        sequence.Add(elements[index]);
                        index++;
                    }
                }
                else if (IsEmojiComponent(next)) // VS16, skin tone, keycap, tag
                {
                    sequence.Add(next);
                    index++;
                }
                else
                {
                    break;
                }
            }

            return string.Join("", sequence);
        }

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
                var (modCtrl, modAlt, modShift, modWin) = KeyboardService.GetModifierKeysState();
                if (modShift && !modCtrl && !modAlt && !modWin && _lastClickedIndex >= 0)
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
                try { ClipboardWin32Helper.SetText(text); } catch { }
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
            if (!_isPinned)
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

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            if (_isPinned)
            {
                PinButton.Background = (Brush)FindResource("AccentColor");
                PinButton.Foreground = Brushes.White;
            }
            else
            {
                PinButton.Background = Brushes.Transparent;
                PinButton.Foreground = (Brush)FindResource("TextForeground");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void DoCopy()
        {
            if (string.IsNullOrEmpty(SelectedTextBlock.Text)) return;
            try
            {
                ClipboardWin32Helper.SetText(SelectedTextBlock.Text);
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
