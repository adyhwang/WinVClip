#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{

    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "VerticalOffset",
                typeof(double),
                typeof(ScrollViewerBehavior),
                new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static double GetVerticalOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(VerticalOffsetProperty);
        }

        public static void SetVerticalOffset(DependencyObject obj, double value)
        {
            obj.SetValue(VerticalOffsetProperty, value);
        }

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }

    public abstract class BaseConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;
        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
        public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    }

    public class ClipboardTypeConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardType type)
            {
                return type switch
                {
                    ClipboardType.Text => Loc.Get("ClipboardType.Text", "文本"),
                    ClipboardType.Image => Loc.Get("ClipboardType.Image", "图片"),
                    ClipboardType.FileList => Loc.Get("ClipboardType.FileList", "文件"),
                    ClipboardType.RichText => Loc.Get("ClipboardType.RichText", "富文本"),
                    _ => Loc.Get("ClipboardType.Unknown", "未知")
                };
            }
            return Loc.Get("ClipboardType.Unknown", "未知");
        }
    }

    public class ClipboardTypeToBackgroundConverter : BaseConverter
    {
        private static readonly SolidColorBrush TextBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128));
        private static readonly SolidColorBrush ImageBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
        private static readonly SolidColorBrush FileListBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
        private static readonly SolidColorBrush RichTextBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246));
        private static readonly SolidColorBrush DefaultBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardType type)
            {
                return type switch
                {
                    ClipboardType.Text => TextBrush,
                    ClipboardType.Image => ImageBrush,
                    ClipboardType.FileList => FileListBrush,
                    ClipboardType.RichText => RichTextBrush,
                    _ => DefaultBrush
                };
            }
            return DefaultBrush;
        }
    }

    public class BoolToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var boolValue = value is true;
            if (parameter?.ToString() == "Invert")
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    public class BoolToOpacityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var (enabledOpacity, disabledOpacity) = ParseOpacityParameters(parameter);
            return value is true ? enabledOpacity : disabledOpacity;
        }

        private static (double enabled, double disabled) ParseOpacityParameters(object parameter)
        {
            if (parameter == null) return (1.0, 0.4);

            var parts = parameter.ToString()?.Split(',');
            if (parts?.Length == 2 && 
                double.TryParse(parts[0], out var enabled) && 
                double.TryParse(parts[1], out var disabled))
            {
                return (enabled, disabled);
            }
            return (1.0, 0.4);
        }

        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class IsRichTextConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.RichText
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class RichTextHasImageConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.RichText && !string.IsNullOrEmpty(item.ImagePath)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class IsImageConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.Image
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class ItemTypeToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.Text
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class IsTextOrRichTextConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class ItemTypeToFolderVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ClipboardItem item)
                return Visibility.Collapsed;

            if (item.Type == ClipboardType.FileList && item.FilePaths?.Count > 0)
                return Visibility.Visible;

            if (item.IsLocalPath)
                return Visibility.Visible;

            return Visibility.Collapsed;
        }
    }

    public class ItemTypeToFileVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.FileList
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class TypeToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardType.Image ? Visibility.Visible : Visibility.Collapsed;
    }

    public class StringToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>筛选按钮激活态判定：ConverterParameter 编码目标筛选类型，3 为链接筛选。</summary>
    internal static class FilterState
    {
        public static bool IsActive(int? currentFilter, int targetTypeValue) => targetTypeValue switch
        {
            -1 => !currentFilter.HasValue,
            0 => currentFilter == (int)ClipboardType.Text,
            1 => currentFilter == (int)ClipboardType.Image,
            2 => currentFilter == (int)ClipboardType.FileList,
            3 => currentFilter == FilterType.Link,
            _ => false
        };
    }

    public class TypeFilterToBackgroundConverter : BaseConverter
    {
        private static readonly SolidColorBrush ActiveFilterBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(parameter?.ToString(), out int targetTypeValue))
            {
                int? currentFilter = value as int?;
                bool isActive = FilterState.IsActive(currentFilter, targetTypeValue);

                if (isActive)
                    return ActiveFilterBrush;
            }
            return Application.Current.FindResource("ItemBackground");
        }
    }

    public class TypeFilterToForegroundConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(parameter?.ToString(), out int targetTypeValue))
            {
                int? currentFilter = value as int?;
                bool isActive = FilterState.IsActive(currentFilter, targetTypeValue);

                if (isActive)
                    return System.Windows.Media.Brushes.White;
            }
            return Application.Current.FindResource("TextForeground");
        }
    }

    public class GroupTab : INotifyPropertyChanged
    {
        public long GroupId { get; set; }

        private string _displayName = "";
        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        private string _toolTip = "";
        public string ToolTip
        {
            get => _toolTip;
            set
            {
                _toolTip = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTip)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static string GetAbbreviation(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var firstChar = name[0];
            if (firstChar > 0x1F000 || (firstChar >= 0x2600 && firstChar <= 0x27BF) || (firstChar >= 0x2702 && firstChar <= 0x27B0))
                return firstChar.ToString();
            if (firstChar > 0x4E00)
                return firstChar.ToString();
            if (char.IsLetter(firstChar) && name.Length >= 2 && char.IsLetter(name[1]))
                return name.Substring(0, 2).ToUpper();
            return firstChar.ToString();
        }
    }

    /// <summary>分组名显示转换：内置默认分组（库中名为 Favorite）按当前语言展示。</summary>
    public class GroupNameConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Loc.GetGroupName(value?.ToString());
    }

    public class EditMenuVisibilityConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is ClipboardItem item && values[1] is MainViewModel viewModel)
            {
                if (viewModel.IsMultiSelectMode)
                    return Visibility.Collapsed;

                if (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class PreviewTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ClipboardItem clipboardItem)
            {
                return clipboardItem.Type switch
                {
                    ClipboardType.Text => TextTemplate,
                    ClipboardType.Image => ImageTemplate,
                    ClipboardType.FileList => FileTemplate,
                    _ => TextTemplate
                };
            }
            return base.SelectTemplate(item, container);
        }
    }

    public class TextPreviewConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string content || content.Length == 0)
                return value;

            const int maxCharsPerLine = 200;
            const int maxLines = 10;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool isTruncated = false;
            string result;

            if (lines.Length == 1 && content.Length > maxCharsPerLine)
            {
                result = content.Substring(0, maxCharsPerLine);
                isTruncated = true;
            }
            else
            {
                var previewLines = lines.Take(maxLines).ToList();
                result = string.Join("\n", previewLines);
                isTruncated = lines.Length > maxLines;
            }

            var suffix = isTruncated ? "……" : "";
            return $"{result}\n{suffix}{string.Format(Loc.Get("Preview.CharCount", "(共{0}个字符)"), content.Length)}";
        }
    }

    public class FileListPreviewConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not List<string> filePaths || filePaths.Count == 0)
                return value;

            var previewPaths = filePaths.Take(10).Select(FormatFilePath).ToList();
            var result = string.Join("\n", previewPaths);

            if (filePaths.Count > 10)
            {
                result += "\n" + GetFileCountSuffix(filePaths);
            }

            return result;
        }

        private static string FormatFilePath(string path)
        {
            var fileName = Path.GetFileName(path);
            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(directory) ? fileName : $"{fileName}  ({directory})";
        }

        private static string GetFileCountSuffix(List<string> filePaths)
        {
            int folderCount = 0, fileCount = 0;
            foreach (var p in filePaths)
            {
                if (Directory.Exists(p)) folderCount++;
                else fileCount++;
            }

            return folderCount > 0 && fileCount > 0
                ? string.Format(Loc.Get("Preview.FilesAndFolders", "……(共{0}个文件、{1}个目录)"), fileCount, folderCount)
                : folderCount > 0
                    ? string.Format(Loc.Get("Preview.Folders", "……(共{0}个目录)"), folderCount)
                    : string.Format(Loc.Get("Preview.Files", "……(共{0}个文件)"), fileCount);
        }
    }

    public class ImageCache
    {
        private static readonly int MaxCacheSize = 50;
        private static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();
        private static readonly LinkedList<string> _lruList = new LinkedList<string>();
        private static readonly object _lock = new object();

        public static BitmapImage GetImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);

            lock (_lock)
            {
                if (_cache.TryGetValue(fullPath, out var cachedImage))
                {
                    _lruList.Remove(fullPath);
                    _lruList.AddFirst(fullPath);
                    return cachedImage;
                }

                try
                {
                    if (File.Exists(fullPath))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(fullPath);
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 500;
                        image.EndInit();
                        image.Freeze();

                        while (_cache.Count >= MaxCacheSize && _lruList.Count > 0)
                        {
                            var oldest = _lruList.Last.Value;
                            _lruList.RemoveLast();
                            _cache.Remove(oldest);
                        }

                        _cache[fullPath] = image;
                        _lruList.AddFirst(fullPath);
                        return image;
                    }
                }
                catch
                {
                }

                return null;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
            }
        }
    }

    public class ImagePathToImageConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string imagePath)
            {
                return ImageCache.GetImage(imagePath);
            }
            return null;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private readonly FileValidationService _fileValidationService;
        private readonly System.Timers.Timer _searchDelayTimer;

        public RangeObservableCollection<ClipboardItem> ClipboardItems { get; } = new RangeObservableCollection<ClipboardItem>();

        public string Hotkey => _settingsService.Settings.Hotkey;

        public string ImageBadgeText => Loc.Get("MainWindow.Badge.Image", "图片");
        public string InvalidFileBadgeText => Loc.Get("MainWindow.Badge.InvalidFile", "失效");

        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
                App.GetWindowStateService()?.SetPinned(value);
                OnPropertyChanged();
            }
        }

        private bool _isMultiSelectMode = false;
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                _isMultiSelectMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BottomRecordStats));
                if (!_isMultiSelectMode)
                {
                    ClearSelection();
                }
            }
        }

        private HashSet<long> _selectedItemIds = new HashSet<long>();
        public HashSet<long> SelectedItemIds
        {
            get => _selectedItemIds;
            set
            {
                _selectedItemIds = value;
                OnPropertyChanged();
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchKeywordsText));
                _searchDelayTimer?.Stop();
                _searchDelayTimer?.Start();
            }
        }

        // ===== 搜索选项（持久化在 settings.json，Popup 与设置页共用） =====

        public bool IsSearchHighlightEnabled
        {
            get => _settingsService.Settings.SearchHighlightEnabled;
            set
            {
                _settingsService.Settings.SearchHighlightEnabled = value;
                _settingsService.SaveSettings();
                OnPropertyChanged();
            }
        }

        public bool IsSearchExactEnabled
        {
            get => _settingsService.Settings.SearchExactModeEnabled;
            set
            {
                _settingsService.Settings.SearchExactModeEnabled = value;
                _settingsService.SaveSettings();
                OnPropertyChanged();
                LoadItems();
            }
        }

        public bool IsSearchHistoryEnabled
        {
            get => _settingsService.Settings.SearchHistoryEnabled;
            set
            {
                _settingsService.Settings.SearchHistoryEnabled = value;
                _settingsService.SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchHistoryVisible));
            }
        }

        public bool IsSearchPinyinEnabled
        {
            get => _settingsService.Settings.SearchPinyinEnabled;
            set
            {
                _settingsService.Settings.SearchPinyinEnabled = value;
                _settingsService.SaveSettings();
                OnPropertyChanged();
                LoadItems();
            }
        }

        public bool IsSearchRegexEnabled
        {
            get => _settingsService.Settings.SearchRegexEnabled;
            set
            {
                _settingsService.Settings.SearchRegexEnabled = value;
                if (value && _settingsService.Settings.SearchWildcardEnabled)
                {
                    _settingsService.Settings.SearchWildcardEnabled = false;
                    OnPropertyChanged(nameof(IsSearchWildcardEnabled));
                }
                _settingsService.SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchModeText));
                LoadItems();
            }
        }

        public bool IsSearchWildcardEnabled
        {
            get => _settingsService.Settings.SearchWildcardEnabled;
            set
            {
                _settingsService.Settings.SearchWildcardEnabled = value;
                if (value && _settingsService.Settings.SearchRegexEnabled)
                {
                    _settingsService.Settings.SearchRegexEnabled = false;
                    OnPropertyChanged(nameof(IsSearchRegexEnabled));
                }
                _settingsService.SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchModeText));
                LoadItems();
            }
        }

        public System.Windows.Visibility IsSearchHistoryVisible =>
            IsSearchHistoryEnabled ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        /// <summary>搜索历史（返回副本，确保集合变化时绑定刷新；Popup 打开前由外部触发通知）</summary>
        public List<string> SearchHistoryView => _settingsService.Settings.SearchHistory.ToList();

        /// <summary>当前搜索词（原样，供高亮行为绑定）</summary>
        public string SearchKeywordsText => SearchText ?? string.Empty;

        /// <summary>高亮匹配模式：Normal（分词）/ Regex / Wildcard</summary>
        public string SearchModeText =>
            _settingsService.Settings.SearchRegexEnabled ? "Regex"
            : _settingsService.Settings.SearchWildcardEnabled ? "Wildcard"
            : "Normal";

        /// <summary>由设置派生当前搜索选项</summary>
        private Models.SearchOptions BuildSearchOptions()
        {
            var s = _settingsService.Settings;
            return new Models.SearchOptions
            {
                ExactMode = s.SearchExactModeEnabled,
                UseRegex = s.SearchRegexEnabled,
                UseWildcard = s.SearchWildcardEnabled,
                UsePinyin = s.SearchPinyinEnabled
            };
        }

        /// <summary>记录搜索历史（去重、上限 10 条、持久化；按 Enter 或搜索框失焦时调用）</summary>
        public void RecordSearchHistory()
        {
            var s = _settingsService.Settings;
            var term = SearchText?.Trim();
            if (string.IsNullOrEmpty(term) || !s.SearchHistoryEnabled)
                return;
            s.SearchHistory.Remove(term);
            s.SearchHistory.Insert(0, term);
            if (s.SearchHistory.Count > 10)
                s.SearchHistory.RemoveAt(s.SearchHistory.Count - 1);
            _settingsService.SaveSettings();
            OnPropertyChanged(nameof(SearchHistoryView));
        }

        /// <summary>清空搜索历史</summary>
        public void ClearSearchHistory()
        {
            _settingsService.Settings.SearchHistory.Clear();
            _settingsService.SaveSettings();
            OnPropertyChanged(nameof(SearchHistoryView));
        }

        private int? _typeFilter;
        public int? TypeFilter
        {
            get => _typeFilter;
            set
            {
                _typeFilter = value;
                OnPropertyChanged();
                LoadItems();
            }
        }

        private long? _groupFilter;
        public long? GroupFilter
        {
            get => _groupFilter;
            set
            {
                _groupFilter = value;
                OnPropertyChanged();
                LoadItems();
            }
        }

        public bool IsAnyFilterActive => TypeFilter.HasValue || GroupFilter.HasValue;

        private bool _isFilterPanelVisible = false;
        public bool IsFilterPanelVisible
        {
            get => _isFilterPanelVisible;
            set
            {
                _isFilterPanelVisible = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Group> Groups { get; } = new ObservableCollection<Group>();

        public ObservableCollection<GroupTab> GroupTabs { get; } = new ObservableCollection<GroupTab>();

        private int _selectedGroupTabIndex = 0;
        public int SelectedGroupTabIndex
        {
            get => _selectedGroupTabIndex;
            set
            {
                _selectedGroupTabIndex = value;
                OnPropertyChanged();
            }
        }

        public void LoadGroups()
        {
            Groups.Clear();
            GroupTabs.Clear();
            var groups = _databaseService.GetAllGroups();
            var allName = Loc.Get("Common.All", "全部");
            Groups.Add(new Group { Id = 0, Name = allName });
            GroupTabs.Add(new GroupTab { GroupId = 0, DisplayName = "📑", ToolTip = allName });
            foreach (var group in groups)
            {
                Groups.Add(group);
                var displayName = !string.IsNullOrEmpty(group.Icon) ? group.Icon : GroupTab.GetAbbreviation(group.Name);
                GroupTabs.Add(new GroupTab { GroupId = group.Id, DisplayName = displayName, ToolTip = Loc.GetGroupName(group.Name) });
            }
            GroupTabs.Add(new GroupTab { GroupId = -1, DisplayName = "➕", ToolTip = Loc.Get("MainWindow.GroupManage", "分组管理") });
            SelectedGroupTabIndex = 0;
        }

        /// <summary>语言切换后刷新分组标签的提示文案，保留当前选中项。</summary>
        public void RefreshGroupTabTexts()
        {
            foreach (var tab in GroupTabs)
            {
                if (tab.GroupId == -1)
                {
                    tab.ToolTip = Loc.Get("MainWindow.GroupManage", "分组管理");
                }
                else if (tab.GroupId == 0)
                {
                    tab.ToolTip = Loc.Get("Common.All", "全部");
                }
                else
                {
                    var group = Groups.FirstOrDefault(g => g.Id == tab.GroupId);
                    if (group != null)
                        tab.ToolTip = Loc.GetGroupName(group.Name);
                }
            }
        }

        public bool HasItems => ClipboardItems.Count > 0;

        public bool IsEmpty => ClipboardItems.Count == 0;

        public int TotalCount
        {
            get => field;
            set
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BottomRecordStats));
            }
        }

        /// <summary>左下角统计：格式为「已加载数 / 总数 条」</summary>
        public string BottomRecordStats
        {
            get
            {
                string records = string.Format(Loc.Get("MainWindow.Bottom.LoadedOfTotal", "{0} / {1} 条"), ClipboardItems.Count, TotalCount);
                if (!IsMultiSelectMode)
                    return records;

                string selected = string.Format(Loc.Get("MainWindow.Bottom.SelectedCount", "选中 {0} 个项目"), SelectedItemIds.Count);
                return string.Format(Loc.Get("MainWindow.Bottom.RecordsAndSelected", "{0} | {1}"), records, selected);
            }
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoadingMore = false;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set
            {
                _isLoadingMore = value;
                OnPropertyChanged();
            }
        }

        private bool _hasMoreItems = true;
        public bool HasMoreItems
        {
            get => _hasMoreItems;
            set
            {
                _hasMoreItems = value;
                OnPropertyChanged();
            }
        }

        private int _currentOffset = 0;
        private const int PageSize = 30;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ItemAdded;

        // 数据版本号：AddItem/RemoveItem 时递增，用于检测列表加载期间的并发写入
        private int _dataVersion;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainViewModel(DatabaseService databaseService, SettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            _fileValidationService = new FileValidationService();
            _searchDelayTimer = new System.Timers.Timer(300);
            _searchDelayTimer.AutoReset = false;
            _searchDelayTimer.Elapsed += (s, e) => Application.Current.Dispatcher.Invoke(LoadItems);
            LoadItems();
        }

        public async Task LoadItemsAsync()
        {
            IsLoading = true;
            _currentOffset = 0;
            HasMoreItems = true;
            int startVersion = _dataVersion;
            bool stale = false;
            try
            {
                List<ClipboardItem> newItems = null;
                int totalCount = 0;
                var searchOptions = BuildSearchOptions();
                await Task.Run(() =>
                {
                    if (TypeFilter == FilterType.Link)
                    {
                        newItems = _databaseService.GetLinkItems(PageSize, _currentOffset, SearchText,
                            GroupFilter, searchOptions);
                        totalCount = _databaseService.GetLinkTotalCount(SearchText, GroupFilter, searchOptions);
                    }
                    else
                    {
                        newItems = _databaseService.GetItems(PageSize, _currentOffset, SearchText,
                            TypeFilter, GroupFilter, searchOptions);
                        totalCount = _databaseService.GetTotalCount(SearchText, TypeFilter, GroupFilter, searchOptions);
                    }
                });

                if (newItems == null)
                    return;

                _currentOffset = newItems.Count;
                HasMoreItems = newItems.Count >= PageSize;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ClipboardItems.Count == 0)
                        ClipboardItems.AddRange(newItems);
                    else
                        ClipboardItems.ReplaceAll(newItems);

                    foreach (var item in newItems)
                    {
                        item.IsSelected = SelectedItemIds.Contains(item.Id);
                    }

                    OnPropertyChanged(nameof(HasItems));
                    OnPropertyChanged(nameof(IsEmpty));

                    _fileValidationService.ValidateItems(newItems);

                    TotalCount = totalCount;
                });

                stale = _dataVersion != startVersion;
            }
            catch (Exception)
            {
                HasMoreItems = false;
            }
            finally
            {
                IsLoading = false;
            }

            // 加载期间有记录增删：首页快照已过期，重载一次
            if (stale)
                _ = LoadItemsAsync();
        }

        public async Task LoadMoreItemsAsync()
        {
            if (IsLoadingMore || !HasMoreItems)
                return;

            IsLoadingMore = true;
            try
            {
                List<ClipboardItem> newItems = null;
                var searchOptions = BuildSearchOptions();
                await Task.Run(() =>
                {
                    if (TypeFilter == FilterType.Link)
                    {
                        newItems = _databaseService.GetLinkItems(PageSize, _currentOffset, SearchText,
                            GroupFilter, searchOptions);
                    }
                    else
                    {
                        newItems = _databaseService.GetItems(PageSize, _currentOffset, SearchText,
                            TypeFilter, GroupFilter, searchOptions);
                    }
                });

                if (newItems == null || newItems.Count == 0)
                {
                    HasMoreItems = false;
                    return;
                }

                _currentOffset += newItems.Count;
                HasMoreItems = newItems.Count >= PageSize;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ClipboardItems.AddRange(newItems);

                    foreach (var item in newItems)
                    {
                        item.IsSelected = SelectedItemIds.Contains(item.Id);
                    }

                    _fileValidationService.ValidateItems(newItems);
                    OnPropertyChanged(nameof(BottomRecordStats));
                });
            }
            catch (Exception)
            {
                HasMoreItems = false;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        public void ReValidateAllFiles()
        {
            _fileValidationService.ReValidateAll(ClipboardItems);
        }

        public void ReleaseMemory()
        {
            foreach (var item in ClipboardItems)
            {
                item.ReleaseMemory();
            }
            ClipboardItems.Clear();
            OnPropertyChanged(nameof(BottomRecordStats));
            _currentOffset = 0;
            HasMoreItems = true;
            SelectedItemIds.Clear();
        }

        public void TrimLargeObjects()
        {
            foreach (var item in ClipboardItems)
            {
                item.TrimLargeObjects();
            }
        }

        public void LoadItems()
        {
            _ = LoadItemsAsync();
        }

        public void ToggleSelection(ClipboardItem item)
        {
            var isSelected = SelectedItemIds.Contains(item.Id);
            if (isSelected)
                SelectedItemIds.Remove(item.Id);
            else
                SelectedItemIds.Add(item.Id);

            item.IsSelected = !isSelected;
            OnPropertyChanged(nameof(SelectedItemIds));
            OnPropertyChanged(nameof(BottomRecordStats));
        }

        public void ClearSelection()
        {
            foreach (var item in ClipboardItems.Where(i => i.IsSelected))
            {
                item.IsSelected = false;
            }
            SelectedItemIds.Clear();
            OnPropertyChanged(nameof(SelectedItemIds));
            OnPropertyChanged(nameof(BottomRecordStats));
        }

        public List<ClipboardItem> GetSelectedItems()
        {
            return ClipboardItems.Where(item => SelectedItemIds.Contains(item.Id)).ToList();
        }

        public void AddItem(ClipboardItem item)
        {

            ClipboardItems.Insert(0, item);
            if (ClipboardItems.Count > 200)
            {
                ClipboardItems.RemoveAt(ClipboardItems.Count - 1);
            }
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));

            TotalCount++;
            _dataVersion++;

            if (item.Type == ClipboardType.FileList)
                _fileValidationService.ValidateItem(item);

            ItemAdded?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveItem(ClipboardItem item)
        {
            ClipboardItems.Remove(item);
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));

            if (TotalCount > 0) TotalCount--;
            _dataVersion++;
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private bool _isVisible = false;

        /// <summary>主界面当前是否可见（供 App 层门控剪贴板实时 UI 更新）</summary>
        public bool IsMainWindowVisible => _isVisible;
        private HwndSource _hwndSource;
        private double _savedLeft;
        private double _savedTop;
        private System.Windows.Point _dragStartPoint;

        private bool _isDragStarted;

        private System.Windows.Threading.DispatcherTimer _tooltipTimer;
        private ClipboardItem _currentTooltipItem;
        private Border _currentTooltipBorder;
        private bool _isPasting = false;
        private IntPtr _lastFocusHwndWhenShow = IntPtr.Zero;
        private IntPtr _capturedTargetHwnd = IntPtr.Zero;

        // 顺序粘贴状态：后进先出/先进先出各自独立维护队列、索引与上次按键时间，互不影响
        private List<ClipboardItem> _seqPasteQueueLifo;
        private int _seqPasteIndexLifo;
        private DateTime _seqPasteLastPressLifo = DateTime.MinValue;

        private List<ClipboardItem> _seqPasteQueueFifo;
        private int _seqPasteIndexFifo;
        private DateTime _seqPasteLastPressFifo = DateTime.MinValue;

        // 顺序粘贴异步执行守卫：执行期间的新按键只暂存一个待处理按键，避免并发交错
        private bool _seqPasteBusy;
        private bool _seqPasteHasPending;
        private bool _seqPastePendingFifo;
        private DateTime _seqPastePendingTime;
        private List<Models.QuickPasteShortcut> _quickPasteShortcuts = new List<Models.QuickPasteShortcut>();

        private System.Windows.Threading.DispatcherTimer _pasteAndDeleteClearTimer;
        private System.Windows.Threading.DispatcherTimer _bottomActionDelayTimer;

        private string _pasteAndDeleteClearText;

        private List<string> _pasteAndDeleteClearFiles;

        private System.Threading.Timer _backgroundGcTimer;

        // 主窗口尺寸去抖落盘定时器：拖拽调整过程中 SizeChanged 会连续触发，避免反复写盘
        private System.Windows.Threading.DispatcherTimer _windowSizeSaveTimer;

        private DoubleAnimation _currentScrollAnimation;
        private bool _isAnimatingScroll = false;
        private ScrollViewer _listBoxScrollViewer;

        private int _panelState = 0;
        private List<CharGroupData> _charGroups = new List<CharGroupData>();
        private bool _charPanelBuilt = false;

        public bool IsPanelActive
        {
            get => _panelState != 0;
        }

        public MainViewModel ViewModel => _viewModel;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow(DatabaseService databaseService, SettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            _viewModel = new MainViewModel(databaseService, settingsService);

            DataContext = _viewModel;
            InitializeComponent();

            ClipboardListBox.Loaded += (s, e) =>
            {
                _listBoxScrollViewer = FindVisualChild<ScrollViewer>(ClipboardListBox);
            };

            ApplyLocalization();
            ApplyFontSize();
            _charGroups = LoadPanelDataRaw("Characters", "characters.json", "WinVClip.Resources.Characters.characters.json") ?? new List<CharGroupData>();
            _viewModel.LoadGroups();

            RestoreWindowSize();
            _savedLeft = Left;
            _savedTop = Top;

            // 尺寸恢复之后再订阅，避免布局阶段的 SizeChanged 把默认尺寸写回设置
            SizeChanged += MainWindow_SizeChanged;
            Closed += MainWindow_Closed;

            SourceInitialized += MainWindow_SourceInitialized;
            _settingsService.SettingsChanged += OnSettingsChanged;
            _viewModel.ItemAdded += OnItemAdded;
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            LoadQuickPasteShortcuts();
        }

        private void LoadQuickPasteShortcuts()
        {
            _quickPasteShortcuts = _settingsService.Settings.QuickPasteShortcuts?
                .Select(s => s.Clone()).ToList() ?? new List<Models.QuickPasteShortcut>();
        }

        private void ApplyLocalization()
        {
            this.LocalizeAll(
                (FilterToggleButton, "MainWindow.ToolTip.Filter", "筛选", LocProp.ToolTip),
                (SettingsButton, "MainWindow.ToolTip.Settings", "设置", LocProp.ToolTip),
                (PinButton, "MainWindow.ToolTip.Pin", "置顶", LocProp.ToolTip),
                (CloseButton, "MainWindow.ToolTip.Close", "关闭 (ESC)", LocProp.ToolTip),

                (FilterAllButton, "MainWindow.Filter.All", "全部", LocProp.ToolTip),
                (FilterTextButton, "MainWindow.Filter.Text", "文本", LocProp.ToolTip),
                (FilterLinkButton, "MainWindow.Filter.Link", "链接", LocProp.ToolTip),
                (FilterImageButton, "MainWindow.Filter.Image", "图片", LocProp.ToolTip),
                (FilterFileButton, "MainWindow.Filter.File", "文件", LocProp.ToolTip),
                (PanelToggleButton, "Panel.ToolTip.Toggle", "字符面板", LocProp.ToolTip));

            this.LocalizeAll(
                (MenuPasteWithFormat, "MainWindow.ContextMenu.PasteWithFormat", "带格式粘贴"),
                (MenuPasteAsText, "MainWindow.ContextMenu.PasteAsText", "纯文本粘贴"),
                (MenuPasteRemoveNewlines, "MainWindow.ContextMenu.PasteRemoveNewlines", "纯文本去换行粘贴"),
                (MenuPasteAndDelete, "MainWindow.ContextMenu.PasteAndDelete", "粘贴后删除"),
                (MenuPasteAsImage, "MainWindow.ContextMenu.PasteAsImage", "图片粘贴"),
                (MenuImagePaste, "MainWindow.ContextMenu.ImagePaste", "图片粘贴"),
                (MenuImageFilePaste, "MainWindow.ContextMenu.ImageFilePaste", "图片文件粘贴"),
                (MenuOpenInBrowser, "MainWindow.ContextMenu.OpenInBrowser", "在浏览器打开"),
                (MenuOpenFolder, "MainWindow.ContextMenu.OpenFolder", "打开文件夹"),
                (MenuEdit, "MainWindow.ContextMenu.Edit", "编辑"),
                (MenuQuickCommands, "MainWindow.ContextMenu.QuickCommands", "快捷命令"),
                (MenuCopy, "MainWindow.ContextMenu.Copy", "复制"),
                (MenuGroup, "MainWindow.ContextMenu.Group", "分组"),
                (MenuDelete, "MainWindow.ContextMenu.Delete", "删除"),
                (MenuBatchDelete, "MainWindow.ContextMenu.BatchDelete", "批量删除"),
                (MenuBatchPaste, "MainWindow.ContextMenu.BatchPaste", "批量粘贴"),
                (MenuBatchGroup, "MainWindow.ContextMenu.BatchGroup", "批量分组"),

                (BottomDeleteAllButton, "MainWindow.Bottom.DeleteAll", "全部"),
                (BottomDeleteDuplicatesButton, "MainWindow.Bottom.DeleteDuplicates", "重复"),
                (BottomLabelNormal, "MainWindow.Bottom.DeleteLabel", "删除："),
                (BottomLabelMulti, "MainWindow.Bottom.BatchLabel", "批量："),
                (BottomDeleteAllUngroupedButton, "MainWindow.Bottom.DeleteAllUngrouped", "无分组"),
                (BottomDeleteInvalidButton, "MainWindow.Bottom.DeleteInvalid", "无效记录"),
                (BottomBatchPasteButton, "MainWindow.Bottom.BatchPaste", "粘贴"),
                (BottomBatchDeleteButton, "MainWindow.Bottom.BatchDelete", "删除"),
                (BottomBatchGroupButton, "MainWindow.Bottom.BatchGroup", "分组"),

                (LoadingMoreText, "MainWindow.Status.LoadingMore", "加载更多..."),
                (EmptyStateText, "MainWindow.Status.Empty", "暂无剪贴板记录"),
                (LoadingText, "MainWindow.Status.Loading", "加载中..."),

                (SearchOptionsTitleText, "MainWindow.Search.OptionsTitle", "搜索选项"),
                (OptHighlightCheck, "MainWindow.Search.Highlight", "关键词高亮"),
                (OptExactCheck, "MainWindow.Search.MatchAll", "匹配全部关键词"),
                (OptHistoryCheck, "MainWindow.Search.History", "搜索历史"),
                (OptPinyinCheck, "MainWindow.Search.Pinyin", "拼音搜索"),
                (OptRegexCheck, "MainWindow.Search.Regex", "使用正则"),
                (OptWildcardCheck, "MainWindow.Search.Wildcard", "使用通配符"),
                (SearchHistoryTitleText, "MainWindow.Search.HistoryTitle", "最近搜索"),
                (ClearSearchHistoryButton, "MainWindow.Search.ClearHistory", "清空"),
                (SearchHistoryEmptyText, "MainWindow.Search.HistoryEmpty", "暂无搜索历史"));

            RefreshPanelLocalization(_charGroups, CharTabControl, "CharPanel");
        }

        private void ApplyFontSize()
        {
            var fontSize = _settingsService.Settings.FontSize;
            var scale = fontSize / 14.0;
            ContentContainer.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private void OnItemAdded(object sender, EventArgs e)
        {
            if (!_isVisible)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ClipboardListBox.Items.Count > 0)
                {
                    ClipboardListBox.ScrollIntoView(ClipboardListBox.Items[0]);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnSettingsChanged()
        {
            _viewModel.OnPropertyChanged(nameof(MainViewModel.Hotkey));
            ApplyFontSize();
            LoadQuickPasteShortcuts();
        }

        private void OnLanguageChanged()
        {
            Dispatcher.Invoke(() =>
            {
                ApplyLocalization();
                _viewModel.OnPropertyChanged(nameof(MainViewModel.ImageBadgeText));
                _viewModel.OnPropertyChanged(nameof(MainViewModel.InvalidFileBadgeText));
                _viewModel.OnPropertyChanged(nameof(MainViewModel.BottomRecordStats));
                _viewModel.RefreshGroupTabTexts();
            });
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);

            ClipboardWin32Helper.SetWindowHandle(handle);

            DisableMaximizeAndAeroSnap(handle);

            var focusService = App.GetFocusService();
            if (focusService != null)
            {
                focusService.FocusChanged += OnFocusServiceFocusChanged;
            }
        }

        private void OnFocusServiceFocusChanged(IntPtr hwnd)
        {
            _lastFocusHwndWhenShow = hwnd;
        }

        private void DisableMaximizeAndAeroSnap(IntPtr handle)
        {
            var style = GetWindowLongPtr(handle, GWL_STYLE);
            var newStyle = (IntPtr)(style.ToInt64() & ~WS_MAXIMIZEBOX);
            SetWindowLongPtr(handle, GWL_STYLE, newStyle);
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            const int WM_SHOWMAINWINDOW = 0x0401;
            if (msg == WM_HOTKEY)
            {

                App.ProcessHotkey(wParam.ToInt32());
                handled = true;
                return (IntPtr)1;
            }
            if (msg == WM_SHOWMAINWINDOW)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowAtCursor();
                }));
                handled = true;
                return (IntPtr)1;
            }
            return IntPtr.Zero;
        }

        public void ShowAtCursor()
        {
            var mousePosDevice = GetCursorPosition();
            var hMonitor = MonitorFromPoint(mousePosDevice, MONITOR_DEFAULTTONEAREST);
            var screen = GetScreenFromPoint(mousePosDevice);

            _lastFocusHwndWhenShow = GetForegroundWindow();
            _capturedTargetHwnd = IntPtr.Zero;

            var mousePos = DevicePixelsToLogicalPixels(mousePosDevice, hMonitor);
            var workingArea = DevicePixelsToLogicalPixels(screen.WorkingArea, hMonitor);

            var position = CalculateOptimalWindowPosition(mousePos, workingArea);

            Left = position.X;
            Top = position.Y;

            ShowWithoutActivation();
            _isVisible = true;

            _backgroundGcTimer?.Dispose();
            _backgroundGcTimer = null;

            SavePosition();

            _viewModel.LoadItems();

            if (ClipboardListBox.SelectedItem == null && _viewModel.ClipboardItems.Count > 0)
                ClipboardListBox.SelectedIndex = 0;

            _viewModel.ReValidateAllFiles();

            if (_charPanelBuilt && CharTabControl.SelectedItem is TabItem charTab)
                LoadGroupContent(_charGroups.FirstOrDefault(g => g.Id == charTab.Tag as string) ?? _charGroups.FirstOrDefault(), CharContentPanel, "CharPanel");

            SetGlobalMouseHook();
        }

        private System.Drawing.Point CalculateOptimalWindowPosition(System.Drawing.Point mousePos, System.Drawing.Rectangle workingArea)
        {
            int windowWidth = (int)Width;
            int windowHeight = (int)Height;

            const int margin = 12; 

            int x = mousePos.X + margin;
            int y = mousePos.Y + margin;

            int workRight = workingArea.Left + workingArea.Width;
            int workBottom = workingArea.Top + workingArea.Height;

            if (x + windowWidth > workRight)
            {
                x = mousePos.X - windowWidth - margin;
            }

            if (y + windowHeight > workBottom)
            {
                y = mousePos.Y - windowHeight - margin;
            }

            x = Math.Max(workingArea.Left, Math.Min(x, workRight - windowWidth));
            y = Math.Max(workingArea.Top, Math.Min(y, workBottom - windowHeight));

            return new System.Drawing.Point(x, y);
        }

        private System.Drawing.Point GetCursorPosition()
        {
            var point = new System.Drawing.Point();
            GetCursorPos(ref point);
            return point;
        }

        private System.Drawing.Point DevicePixelsToLogicalPixels(System.Drawing.Point devicePoint, IntPtr monitorHandle)
        {
            var dpi = GetDpiForMonitor(monitorHandle);
            return new System.Drawing.Point(
                (int)(devicePoint.X * 96.0 / dpi.X),
                (int)(devicePoint.Y * 96.0 / dpi.Y));
        }

        private System.Drawing.Rectangle DevicePixelsToLogicalPixels(System.Drawing.Rectangle deviceRect, IntPtr monitorHandle)
        {
            var dpi = GetDpiForMonitor(monitorHandle);
            return new System.Drawing.Rectangle(
                (int)(deviceRect.X * 96.0 / dpi.X),
                (int)(deviceRect.Y * 96.0 / dpi.Y),
                (int)(deviceRect.Width * 96.0 / dpi.X),
                (int)(deviceRect.Height * 96.0 / dpi.Y));
        }

        private (double X, double Y) GetDpiForMonitor(IntPtr hMonitor)
        {
            uint dpiX = 96, dpiY = 96;
            try
            {
                GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, ref dpiX, ref dpiY);
            }
            catch
            {

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    dpiX = dpiY = (uint)GetDpiForWindow(hwnd);
                }
                else
                {

                    dpiX = dpiY = 96;
                }
            }
            return (dpiX, dpiY);
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, MONITOR_DPI_TYPE dpiType, ref uint dpiX, ref uint dpiY);

        private enum MONITOR_DPI_TYPE
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2,
            MDT_DEFAULT = MDT_EFFECTIVE_DPI
        }

        private ScreenInfo GetScreenFromPoint(System.Drawing.Point point)
        {
            var hMonitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
            var info = new MonitorInfo();
            info.cbSize = Marshal.SizeOf(typeof(MonitorInfo));

            if (GetMonitorInfo(hMonitor, ref info))
            {
                return new ScreenInfo
                {
                    WorkingArea = new System.Drawing.Rectangle(
                        info.rcWork.left,
                        info.rcWork.top,
                        info.rcWork.right - info.rcWork.left,
                        info.rcWork.bottom - info.rcWork.top)
                };
            }

            return new ScreenInfo
            {
                WorkingArea = new System.Drawing.Rectangle(0, 0, 
                    (int)System.Windows.SystemParameters.PrimaryScreenWidth, 
                    (int)System.Windows.SystemParameters.PrimaryScreenHeight)
            };
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_MENU = 0x12;

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const int SWP_SHOWWINDOW = 0x0040;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_SIZEBOX = 0x00040000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_FRAMECHANGED = 0x0020;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private bool _isMouseHookSet = false;
        private bool _isContextMenuOpen = false;
        private readonly List<ContextMenu> _openMenus = new List<ContextMenu>();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private class ScreenInfo
        {
            public System.Drawing.Rectangle WorkingArea { get; set; }
        }

        public void ToggleVisibility()
        {
            if (_isVisible)
            {
                Hide();
                _isVisible = false;
            }
            else
            {
                ShowAtCursor();
            }
        }

        /// <summary>恢复上次保存的主窗口尺寸；未保存过（0）则沿用 MainWindow.xaml 的默认值。</summary>
        private void RestoreWindowSize()
        {
            var settings = _settingsService.Settings;
            if (settings.MainWindowWidth > 0)
                Width = Math.Max(settings.MainWindowWidth, MinWidth);
            if (settings.MainWindowHeight > 0)
                Height = Math.Max(settings.MainWindowHeight, MinHeight);
        }

        /// <summary>尺寸变化时先记入内存设置，再用定时器去抖落盘（不触发 SettingsChanged）。</summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var settings = _settingsService.Settings;
            settings.MainWindowWidth = Width;
            settings.MainWindowHeight = Height;

            if (_windowSizeSaveTimer == null)
            {
                _windowSizeSaveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _windowSizeSaveTimer.Tick += (s, args) =>
                {
                    _windowSizeSaveTimer.Stop();
                    _settingsService.SaveSettingsQuiet();
                };
            }

            _windowSizeSaveTimer.Stop();
            _windowSizeSaveTimer.Start();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _windowSizeSaveTimer?.Stop();
            _settingsService.SaveSettingsQuiet();
        }

        private void SavePosition()
        {
            _savedLeft = Left;
            _savedTop = Top;
        }

        private void HideAndSave()
        {
            SavePosition();
            Hide();
            _isVisible = false;

            HideCustomTooltip();
            _tooltipTimer?.Stop();

            _viewModel.TrimLargeObjects();
            ImageCache.Clear();

            if (_charPanelBuilt)
                CharContentPanel.Children.Clear();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            try
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                SetProcessWorkingSetSize(proc.Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }

            _backgroundGcTimer?.Dispose();
            _backgroundGcTimer = new System.Threading.Timer(_ =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, true);
                try
                {
                    using var proc = System.Diagnostics.Process.GetCurrentProcess();
                    SetProcessWorkingSetSize(proc.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

            if (_listBoxScrollViewer != null)
            {
                _listBoxScrollViewer.ScrollToVerticalOffset(0);
            }

            RemoveGlobalMouseHook();
        }

        private void SetWindowActivateStyle(bool activate)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLongPtr(handle, GWL_EXSTYLE);
            var newStyle = activate 
                ? (IntPtr)(exStyle.ToInt64() & ~WS_EX_NOACTIVATE)
                : (IntPtr)(exStyle.ToInt64() | WS_EX_NOACTIVATE);
            SetWindowLongPtr(handle, GWL_EXSTYLE, newStyle);
        }

        private void ActivateWindow()
        {
            SetWindowActivateStyle(true);
            Activate();
            Focus();
        }

        private void DeactivateWindow()
        {
            if (!_isVisible) return;

            var handle = new WindowInteropHelper(this).Handle;
            SetWindowActivateStyle(false);

            if (_viewModel.IsPinned)
            {
                SetWindowPos(handle, (IntPtr)(-1), 0, 0, 0, 0, 
                    SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
            }
        }

        private void DeactivateWindowDeferred()
        {
            Dispatcher.BeginInvoke(new Action(DeactivateWindow), 
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void SearchTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ActivateWindow();
            SearchTextBox.Focus();

            // 浮层仅在搜索框为空时显示
            if (!string.IsNullOrEmpty(SearchTextBox.Text))
            {
                e.Handled = false;
                return;
            }

            // 已打开时：本次点击按“外部点击”关闭浮层（点击搜索栏 = 切换）
            if (SearchOptionsPopup.IsOpen)
            {
                e.Handled = false;
                return;
            }

            // Popup 以 StaysOpen=False 打开时本次点击会被判为外部点击关闭，故延迟到 mouseup 之后再打开
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!SearchOptionsPopup.IsOpen)
                    OpenSearchOptionsPopup();
            }), System.Windows.Threading.DispatcherPriority.Input);

            e.Handled = false;
        }

        /// <summary>按 Enter 时保存搜索历史</summary>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.RecordSearchHistory();
                _viewModel.OnPropertyChanged(nameof(MainViewModel.SearchHistoryView));
            }
        }

        /// <summary>搜索框失焦（点击其他任意位置）时保存搜索历史</summary>
        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.RecordSearchHistory();
            _viewModel.OnPropertyChanged(nameof(MainViewModel.SearchHistoryView));
        }

        /// <summary>输入时隐藏浮层，清空后重新显示</summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchTextBox.Text))
            {
                if (!SearchOptionsPopup.IsOpen && SearchTextBox.IsKeyboardFocusWithin)
                    OpenSearchOptionsPopup();
            }
            else if (SearchOptionsPopup.IsOpen)
            {
                SearchOptionsPopup.IsOpen = false;
            }
        }

        /// <summary>打开搜索选项浮层（重新读取共享 Settings 实例，刷新选项勾选态与历史列表；选项区默认收起）</summary>
        private void OpenSearchOptionsPopup()
        {
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchHighlightEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchExactEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchHistoryEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchPinyinEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchRegexEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchWildcardEnabled));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.SearchModeText));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.SearchHistoryView));
            _viewModel.OnPropertyChanged(nameof(MainViewModel.IsSearchHistoryVisible));
            CollapseSearchOptionsPanel();
            SearchOptionsPopup.IsOpen = true;
        }

        /// <summary>点击「搜索选项」标题展开/收起选项区</summary>
        private void SearchOptionsTitle_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            bool expand = SearchOptionsPanel.Visibility != Visibility.Visible;
            SearchOptionsPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            SearchOptionsArrow.Text = expand ? "▾" : "▸";
        }

        /// <summary>选项区收起并复位箭头</summary>
        private void CollapseSearchOptionsPanel()
        {
            SearchOptionsPanel.Visibility = Visibility.Collapsed;
            SearchOptionsArrow.Text = "▸";
        }

        /// <summary>清空搜索框内容（TextChanged 触发防抖刷新，恢复全部记录）</summary>
        private void ClearSearchText_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
        }

        private void ClearSearchHistory_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearSearchHistory();
            _viewModel.OnPropertyChanged(nameof(MainViewModel.SearchHistoryView));
        }

        private void SearchHistoryTerm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string term)
            {
                SearchOptionsPopup.IsOpen = false;
                _viewModel.SearchText = term;
            }
        }

        private void Button_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is Button button)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void ItemBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SetWindowActivateStyle(true);
            e.Handled = false;
            DeactivateWindowDeferred();

            if (e.ChangedButton == MouseButton.Left)
            {
                _dragStartPoint = e.GetPosition(this);
                _isDragStarted = false;
            }

            if (!_viewModel.IsMultiSelectMode &&
                (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle))
            {
                _capturedTargetHwnd = ResolvePasteTargetHwnd();
            }
        }

        private IntPtr ResolvePasteTargetHwnd()
        {
            var focusService = App.GetFocusService();

            IntPtr live = focusService?.GetLiveForegroundTarget() ?? IntPtr.Zero;
            if (live != IntPtr.Zero) return live;

            if (_lastFocusHwndWhenShow != IntPtr.Zero)
                return _lastFocusHwndWhenShow;

            return focusService?.LastFocusHwnd ?? IntPtr.Zero;
        }

        private void ItemBorder_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ClipboardItem item)
            {
                if (e.ChangedButton == MouseButton.Middle)
                {

                    if (TryMatchQuickPasteShortcut(item, Models.QuickPasteMouseButton.Middle, out var matched))
                    {
                        e.Handled = true;
                        ExecuteQuickPasteAction(item, matched.Action, matched.QuickCommandName);
                        return;
                    }

                    if (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                    {
                        e.Handled = true;
                        PasteItemAsPlainTextRemoveNewlines(item);
                    }
                }
                else if (e.ChangedButton == MouseButton.Left)
                {

                }
            }
        }

        private void ShowWithoutActivation()
        {
            SetWindowActivateStyle(false);
            Show();
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, (IntPtr)(-1), 0, 0, 0, 0, 
                SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
        }

        public void PreheatRender()
        {
            var savedOpacity = Opacity;
            try
            {
                Opacity = 0;
                SetWindowActivateStyle(false);
                Show();
                UpdateLayout();
            }
            catch
            {
            }
            finally
            {
                try { Hide(); } catch { }
                Opacity = savedOpacity;
            }
        }

        private void SetGlobalMouseHook()
        {
            if (_isMouseHookSet) return;

            _mouseProc = MouseHookProc;
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, 
                GetModuleHandle(null), 0);

            _isMouseHookSet = _mouseHookId != IntPtr.Zero;
        }

        private void RemoveGlobalMouseHook()
        {
            if (_isMouseHookSet && _mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
                _isMouseHookSet = false;
            }
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
                {
                    if (_isContextMenuOpen)
                    {
                        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                    }

                    var mouseHookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var clickPoint = new System.Drawing.Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y);

                    if (!IsPointInWindow(clickPoint))
                    {

                        if (!IsPointInAnyMenu(clickPoint) && !AppMessageBox.IsPointInDialog(clickPoint))
                        {
                            // 低级钩子回调必须立即返回：异步投递到 UI 线程，同步 Invoke 会阻塞鼠标输入，超时后钩子被系统摘除
                            Dispatcher.BeginInvoke(() =>
                            {
                                if (_isVisible && !_viewModel.IsPinned)
                                {
                                    HideWindow();
                                }
                            });
                        }
                    }
                }
            }
            catch
            {
                // 钩子回调内的异常不得外泄到钩子链
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private bool IsPointInAnyMenu(System.Drawing.Point screenPoint)
        {
            foreach (var menu in _openMenus)
            {
                var hwndSource = PresentationSource.FromVisual(menu) as HwndSource;
                if (hwndSource != null)
                {
                    var hwnd = hwndSource.Handle;
                    var menuRect = new RECT();
                    if (GetWindowRect(hwnd, ref menuRect))
                    {
                        if (screenPoint.X >= menuRect.left && 
                            screenPoint.X <= menuRect.right && 
                            screenPoint.Y >= menuRect.top && 
                            screenPoint.Y <= menuRect.bottom)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsPointInWindow(System.Drawing.Point screenPoint)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var windowRect = new RECT();

            if (GetWindowRect(handle, ref windowRect))
            {
                return screenPoint.X >= windowRect.left && 
                       screenPoint.X <= windowRect.right && 
                       screenPoint.Y >= windowRect.top && 
                       screenPoint.Y <= windowRect.bottom;
            }

            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public void HideWindow()
        {
            HideAndSave();
            _viewModel.IsPinned = false;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWindow();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            App.ShowSettingsWindow();
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsPinned = !_viewModel.IsPinned;
            Topmost = _viewModel.IsPinned;
        }

        private void FilterToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsPanelActive)
            {
                _panelState = 0;
                UpdatePanelState();
            }
            _viewModel.IsFilterPanelVisible = !_viewModel.IsFilterPanelVisible;
            if (_viewModel.IsFilterPanelVisible)
            {
                _viewModel.LoadGroups();
            }
            else
            {
                _viewModel.TypeFilter = null;
            }
        }

        private void FilterAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = null;
        }

        private void FilterText_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = (int)ClipboardType.Text;
        }

        private void FilterLink_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = FilterType.Link;
        }

        private void FilterImage_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = (int)ClipboardType.Image;
        }

        private void FilterFile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = (int)ClipboardType.FileList;
        }

        private void GroupTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl && tabControl.SelectedItem is GroupTab tab)
            {
                if (tab.GroupId == -1)
                {
                    _viewModel.SelectedGroupTabIndex = 0;
                    var addGroupWindow = new GroupManageWindow(_databaseService);
                    addGroupWindow.Owner = this;
                    addGroupWindow.ShowDialog();
                    _viewModel.LoadGroups();
                    return;
                }

                if (tab.GroupId == 0)
                    _viewModel.GroupFilter = null;
                else
                    _viewModel.GroupFilter = tab.GroupId;
            }
        }

        private void BottomBar_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_bottomActionDelayTimer != null)
            {
                _bottomActionDelayTimer.Stop();
                _bottomActionDelayTimer = null;
            }
            RightActionPanel.Visibility = Visibility.Visible;
        }

        private void BottomBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (RightActionPanel.Visibility != Visibility.Visible)
                return;

            if (_bottomActionDelayTimer == null)
            {
                _bottomActionDelayTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(400)
                };
                _bottomActionDelayTimer.Tick += (s, args) =>
                {
                    _bottomActionDelayTimer.Stop();
                    _bottomActionDelayTimer = null;
                    RightActionPanel.Visibility = Visibility.Collapsed;
                };
            }
            _bottomActionDelayTimer.Stop();
            _bottomActionDelayTimer.Start();
        }

        private void WindowDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBox or Button or TextBlock)
                return;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private ContextMenu CreateGroupMenu(UIElement placementTarget, long? currentGroupId, Action<long?> onSelect)
        {
            var menu = new ContextMenu { Style = TryFindResource("ContextMenuStyle") as Style };
            menu.PlacementTarget = placementTarget;
            menu.Opened += ContextMenu_Opened;
            menu.Loaded += (s, args) => SetContextMenuTopmost(menu);            

            AddGroupMenuItem(menu, Loc.Get("Common.All", "全部"), null, currentGroupId, onSelect);
            foreach (var group in _databaseService.GetAllGroups())
            {
                AddGroupMenuItem(menu, Loc.GetGroupName(group.Name), group.Id, currentGroupId, onSelect);
            }

            menu.Items.Add(new Separator { Style = TryFindResource("SeparatorStyle") as Style });   

            menu.IsOpen = true;
            return menu;
        }

        private void AddGroupMenuItem(ContextMenu menu, string header, long? groupId, long? currentGroupId, Action<long?> onSelect)
        {
            var item = new MenuItem 
            { 
                Header = header, 
                Tag = groupId, 
                IsCheckable = true,
                Style = TryFindResource("MenuItemStyle") as Style,
                IsChecked = currentGroupId == groupId
            };

            item.Click += (s, args) => 
            {
                onSelect(groupId);
                foreach (var menuItem in menu.Items.OfType<MenuItem>())
                {
                    menuItem.IsChecked = menuItem.Tag as long? == groupId;
                }
            };

            menu.Items.Add(item);
        }

        private void ListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0)
                return;

            if (_isAnimatingScroll)
                return;

            if (!(e.OriginalSource is ScrollViewer scrollViewer))
                return;

            if (scrollViewer.ScrollableHeight <= 0)
                return;

            var threshold = 50;
            var distanceToBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;

            if (distanceToBottom <= threshold)
            {
                _ = _viewModel.LoadMoreItemsAsync();
            }
        }

        private void ClipboardListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {

            var wheelButton = e.Delta > 0 ? Models.QuickPasteMouseButton.WheelUp : Models.QuickPasteMouseButton.WheelDown;
            var hoverItem = GetHoveredClipboardItem();
            if (hoverItem != null && TryMatchQuickPasteShortcut(hoverItem, wheelButton, out var matched))
            {
                e.Handled = true;
                ExecuteQuickPasteAction(hoverItem, matched.Action, matched.QuickCommandName);
                return;
            }

            if (_listBoxScrollViewer != null)
            {
                double targetOffset = _listBoxScrollViewer.VerticalOffset - e.Delta;
                AnimateScrollTo(targetOffset);
                e.Handled = true;
            }
        }

        private ClipboardItem GetHoveredClipboardItem()
        {
            try
            {
                var pos = Mouse.GetPosition(ClipboardListBox);
                var hit = ClipboardListBox.InputHitTest(pos) as DependencyObject;
                while (hit != null && hit != ClipboardListBox)
                {
                    if (hit is FrameworkElement fe && fe.DataContext is ClipboardItem item)
                        return item;
                    hit = VisualTreeHelper.GetParent(hit);
                }
            }
            catch { }
            return null;
        }

        private void AnimateScrollTo(double targetOffset)
        {
            if (_listBoxScrollViewer == null)
                return;

            targetOffset = Math.Max(0, Math.Min(targetOffset, _listBoxScrollViewer.ScrollableHeight));

            if (_currentScrollAnimation != null)
            {
                _listBoxScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, null);
            }

            _isAnimatingScroll = true;

            _currentScrollAnimation = new DoubleAnimation
            {
                From = _listBoxScrollViewer.VerticalOffset,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _currentScrollAnimation.Completed += (s, e) =>
            {
                _isAnimatingScroll = false;
                _currentScrollAnimation = null;

                var distanceToBottom = _listBoxScrollViewer.ScrollableHeight - _listBoxScrollViewer.VerticalOffset;
                if (distanceToBottom <= 50 && _viewModel.HasMoreItems && !_viewModel.IsLoadingMore)
                {
                    _ = _viewModel.LoadMoreItemsAsync();
                }
            };

            _listBoxScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, _currentScrollAnimation);
        }

        private void ClipboardListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsControlElement(e.OriginalSource))
                return;

            if (!_viewModel.IsMultiSelectMode)
            {
                _capturedTargetHwnd = ResolvePasteTargetHwnd();
            }
        }

        private void ClipboardListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsControlElement(e.OriginalSource))
                return;

            if (ClipboardListBox.SelectedItem is ClipboardItem item)
            {

                if (TryMatchQuickPasteShortcut(item, Models.QuickPasteMouseButton.Left, out var matched))
                {
                    e.Handled = true;
                    ExecuteQuickPasteAction(item, matched.Action, matched.QuickCommandName);
                    return;
                }

                if (_viewModel.IsMultiSelectMode)
                    _viewModel.ToggleSelection(item);
                else
                    PasteItemWithKeyboardModifiers(item);
            }
        }

        private static bool IsControlElement(object source)
        {
            return source is Button || source is ScrollBar || source is RepeatButton || source is Thumb;
        }

        private void ClipboardListBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    if (ClipboardListBox.SelectedItem is ClipboardItem deleteItem)
                    {
                        DeleteItem(deleteItem);
                    }
                    break;
                case Key.Escape:
                    HideWindow();
                    break;
            }
        }

        private void OpenInBrowserMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;
            if (item.Type != ClipboardType.Text && item.Type != ClipboardType.RichText)
                return;

            string content = item.Content.Trim();
            string url = IsValidUrl(content) ? EnsureScheme(content) : App.SettingsService.GetSearchUrl(content);

            LaunchExternalProcess(url, "打开浏览器失败");
        }

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;

            string folderPath = null;

            if (item.Type == ClipboardType.FileList && item.FilePaths?.Count > 0)
            {
                string filePath = item.FilePaths[0];
                folderPath = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : filePath;
            }
            else if (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
            {
                string content = item.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    string path = TrimQuotes(content);
                    if (File.Exists(path))
                        folderPath = Path.GetDirectoryName(path);
                    else if (Directory.Exists(path))
                        folderPath = path;
                }
            }

            if (folderPath != null)
                LaunchExternalProcess(folderPath, "打开文件夹失败");
        }

        private static void LaunchExternalProcess(string fileName, string errorMessage)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"{Loc.Get("Message.Error", "错误")}: {ex.Message}", Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string TrimQuotes(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            text = text.Trim();
            if (text.StartsWith("\"") && text.EndsWith("\""))
                return text.Substring(1, text.Length - 2);
            if (text.StartsWith("'") && text.EndsWith("'"))
                return text.Substring(1, text.Length - 2);
            return text;
        }

        /// <summary>判断文本是否为链接（含裸域名与 IPv4，规则见 DatabaseService.LinkPattern）。</summary>
        internal static bool IsValidUrl(string text) => DatabaseService.IsLink(text);

        /// <summary>补全协议头：缺少协议时按 http 补上，供浏览器打开使用。</summary>
        private static string EnsureScheme(string url)
            => url.Contains("://", StringComparison.Ordinal) ? url : "http://" + url;

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;
            if (item.Type != ClipboardType.Text && item.Type != ClipboardType.RichText)
            {
                AppMessageBox.Show(Loc.Get("MainWindow.Message.OnlyTextEdit", "只能编辑文本类型的内容"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var wasRichText = item.Type == ClipboardType.RichText;
            var editWindow = new EditItemWindow(item) { Owner = this };

            var wasPinnedBefore = _viewModel.IsPinned;
            _viewModel.IsPinned = true;
            Topmost = true;

            try
            {
                if (editWindow.ShowDialog() == true)
                {
                    if (wasRichText && item.Type == ClipboardType.Text)
                    {

                        _databaseService.UpdateItemAsText(item.Id, item.Content);
                    }
                    else
                    {
                        _databaseService.UpdateItemContent(item.Id, item.Content);
                    }
                    _viewModel.LoadItems();
                }
            }
            finally
            {

                _viewModel.IsPinned = wasPinnedBefore;
                Topmost = wasPinnedBefore;
            }
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                CopyToClipboard(item);
        }

        private void CharPicker_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;

            string text = item.Type == ClipboardType.RichText && !string.IsNullOrEmpty(item.Content)
                ? item.Content
                : item.Content ?? "";

            if (string.IsNullOrEmpty(text))
                return;

            var targetHwnd = ResolvePasteTargetHwnd();

            var charPickerWindow = new CharPickerWindow(text);
            charPickerWindow.OnInsert += (sender, selectedText) =>
            {
                if (!string.IsNullOrEmpty(selectedText))
                {
                    if (_viewModel.IsPinned)
                        this.Show();
                    PasteTextWithHwnd(selectedText, targetHwnd);
                }
            };
            charPickerWindow.Closed += (_, _) =>
            {
                if (_viewModel.IsPinned)
                    this.Show();
                else
                    this.Hide();
            };
            this.Hide();
            charPickerWindow.Show();
        }

        private void GenerateQRCode_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;

            string text = item.Content ?? "";
            if (string.IsNullOrEmpty(text))
                return;

            if (QRCodeWindow.IsContentTooLarge(text))
            {
                var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
                var msg = string.Format(
                    Loc.Get("QRCode.Error.TooLarge",
                        "内容过长，超出二维码容量上限（最多 {0} 字节，约 {1} 个汉字），生成失败。\n当前内容：{2} 字节（{3} 字符）"),
                    QRCodeWindow.MaxQrBytes, QRCodeWindow.MaxQrBytes / 3, byteCount, text.Length);
                AppMessageBox.Show(msg,
                    Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var qrWindow = new QRCodeWindow(text);
            qrWindow.Closed += (_, _) =>
            {
                if (_viewModel.IsPinned)
                    this.Show();
                else
                    this.Hide();
            };
            this.Hide();
            qrWindow.Show();
        }

        private void MenuCharPicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem)
                menuItem.Header = Loc.Get("MainWindow.ContextMenu.CharPicker", "拆分选字");
        }

        private void MenuGenerateQRCode_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem)
                menuItem.Header = Loc.Get("MainWindow.ContextMenu.GenerateQRCode", "生成二维码");
        }

        private void PasteWithFormat_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                PasteItem(item);
        }

        private void PasteAsText_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                PasteItemAsPlainText(item);
        }

        private void PasteRemoveNewlines_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                PasteItemAsPlainTextRemoveNewlines(item);
        }

        private void PasteAndDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                PasteAndDeleteItem(item);
        }

        private void PasteAndDeleteItem(ClipboardItem item)
        {
            if (_isPasting) return;

            try
            {
                _isPasting = true;

                SetClipboardContent(item);

                var hashText = (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                    ? item.Content ?? "" : null;
                var hashFiles = item.Type == ClipboardType.FileList && item.FilePaths != null
                    ? item.FilePaths : null;

                PostPasteFlow(pasteHashText: hashText, pasteHashFiles: hashFiles);

                DeleteItem(item);

                ScheduleClipboardClearAfterPasteAndDelete(item);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void ScheduleClipboardClearAfterPasteAndDelete(ClipboardItem item)
        {
            _pasteAndDeleteClearText = (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                ? item.Content ?? "" : null;
            _pasteAndDeleteClearFiles = item.Type == ClipboardType.FileList && item.FilePaths != null
                ? item.FilePaths.ToList() : null;

            if (_pasteAndDeleteClearTimer == null)
            {
                _pasteAndDeleteClearTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _pasteAndDeleteClearTimer.Tick += PasteAndDeleteClearTimer_Tick;
            }

            _pasteAndDeleteClearTimer.Stop();
            _pasteAndDeleteClearTimer.Start();
        }

        private void PasteAndDeleteClearTimer_Tick(object sender, EventArgs e)
        {
            _pasteAndDeleteClearTimer.Stop();
            var text = _pasteAndDeleteClearText;
            var files = _pasteAndDeleteClearFiles;
            _pasteAndDeleteClearText = null;
            _pasteAndDeleteClearFiles = null;

            try
            {
                bool isOurs;
                if (text != null)
                    isOurs = ClipboardWin32Helper.ContainsText()
                        && string.Equals(ClipboardWin32Helper.GetText(), text, StringComparison.Ordinal);
                else if (files != null)
                    isOurs = IsClipboardFileListEqual(files);
                else

                    isOurs = ClipboardWin32Helper.ContainsImage();

                if (isOurs)
                    ClipboardWin32Helper.Clear();
            }
            catch
            {

            }
        }

        private static bool IsClipboardFileListEqual(List<string> files)
        {
            if (!ClipboardWin32Helper.ContainsFileDropList())
                return false;

            var dataObject = ClipboardWin32Helper.GetDataObject();
            var current = dataObject?.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (current == null || current.Length != files.Count)
                return false;

            var a = current.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var b = files.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private void PasteAsImage_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item) return;
            if (_isPasting) return;

            try
            {
                _isPasting = true;

                if (string.IsNullOrEmpty(item.ImagePath)) return;

                string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                if (!System.IO.File.Exists(fullPath)) return;

                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(fullPath);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();

                ClipboardWin32Helper.SetImage(image);
                PostPasteFlow(moveTopItem: item);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void ImagePaste_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;

            if (item.Type != ClipboardType.Image)
                return;

            PasteItem(item);
        }

        private void ImageFilePaste_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item) return;
            if (item.Type != ClipboardType.Image) return;
            if (_isPasting) return;

            try
            {
                _isPasting = true;

                if (string.IsNullOrEmpty(item.ImagePath)) return;

                string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                if (!System.IO.File.Exists(fullPath)) return;

                var fileList = new System.Collections.Specialized.StringCollection();
                fileList.Add(fullPath);
                ClipboardWin32Helper.SetFileDropList(fileList);
                PostPasteFlow(moveTopItem: item);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void GroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;

            var menu = CreateGroupMenu(null, item.GroupId, groupId =>
            {
                _databaseService.UpdateItemGroup(item.Id, groupId);
                item.GroupId = groupId;
                _viewModel.LoadItems();
            });
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu)
            {
                _isContextMenuOpen = true;
                _openMenus.Add(contextMenu);
                contextMenu.Closed += ContextMenu_Closed;

                RefreshQuickCommandSubmenu(contextMenu);
                UpdateQuickCommandsTopLevelVisibility(contextMenu);

                if (_viewModel.IsPinned)
                {
                    Topmost = false;
                }

                EnsureContextMenuTopmost(contextMenu);
            }
        }

        private void UpdateQuickCommandsTopLevelVisibility(ContextMenu contextMenu)
        {
            if (MenuQuickCommands == null) return;
            bool hasVisibleItem = false;
            foreach (var item in MenuQuickCommands.Items)
            {
                if (item is FrameworkElement fe)
                {
                    if (fe.Visibility != Visibility.Visible) continue;
                    if (fe is System.Windows.Controls.Separator) continue;
                    if (fe is System.Windows.Controls.MenuItem mi && mi.Tag as string == "__QC_EMPTY__") continue;
                    hasVisibleItem = true;
                    break;
                }
            }
            MenuQuickCommands.Visibility = hasVisibleItem ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshQuickCommandSubmenu(ContextMenu contextMenu)
        {
            if (contextMenu == null || MenuQuickCommands == null) return;
            var parent = MenuQuickCommands.Items;
            if (parent == null) return;

            var curItem = contextMenu.PlacementTarget is System.Windows.Controls.ListBox lb ? lb.SelectedItem as ClipboardItem : null;
            int curType = curItem != null ? (int)curItem.Type : -1;
            bool canApplyToItem = curItem != null && (curItem.Type == ClipboardType.Text || curItem.Type == ClipboardType.RichText);

            int sepIdx = -1;
            for (int i = 0; i < parent.Count; i++)
            {
                if (parent[i] is FrameworkElement fe && fe.Tag as string == "__QC_SEP__")
                {
                    sepIdx = i;
                    break;
                }
            }
            if (sepIdx < 0) return;

            for (int i = parent.Count - 1; i > sepIdx; i--)
                parent.RemoveAt(i);

            var list = App.SettingsService?.Settings?.QuickCommands;
            bool anyUser = false;
            if (list != null)
            {

                foreach (var qc in list)
                {
                    var name = (qc.Name ?? "").Trim();
                    if (name.Length == 0) continue;
                    if (!qc.Enabled) continue;
                    if (curItem != null && !Models.QuickCommand.TypeIncludes(qc.ClipboardType, (int)curItem.Type)) continue;
                    var mi = new System.Windows.Controls.MenuItem
                    {
                        Tag = "QC:" + name,
                        Header = name,
                        Icon = "✨",
                        IsEnabled = canApplyToItem,
                        Style = TryFindResource("MenuItemStyle") as System.Windows.Style
                    };
                    var captured = qc;
                    mi.Click += (s, args) =>
                    {
                        if (ClipboardListBox.SelectedItem is ClipboardItem item)
                        {
                            PasteWithCustomRegex(item, captured.Rules);
                        }
                    };
                    parent.Add(mi);
                    anyUser = true;
                }
            }
            if (!anyUser)
            {
                var empty = new System.Windows.Controls.MenuItem
                {
                    Tag = "__QC_EMPTY__",
                    IsEnabled = false,
                    Header = "(" + Loc.Get("Settings.Hotkey.QuickPaste.QcEmpty", "暂无，请在「快捷命令」tab 添加") + ")",
                    Foreground = TryFindResource("SecondaryTextForeground") as System.Windows.Media.Brush
                                 ?? System.Windows.Media.Brushes.Gray,
                    Style = TryFindResource("MenuItemStyle") as System.Windows.Style
                };
                parent.Add(empty);
            }
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu)
            {
                _openMenus.Remove(contextMenu);

                if (_openMenus.Count == 0)
                {
                    _isContextMenuOpen = false;
                }
            }

            if (_viewModel.IsPinned)
            {
                Topmost = true;
            }
        }

        private async void EnsureContextMenuTopmost(ContextMenu contextMenu)
        {
            for (int i = 0; i < 10; i++)
            {
                SetContextMenuTopmost(contextMenu);
                await System.Threading.Tasks.Task.Delay(10);
            }
        }

        private void SetContextMenuTopmost(ContextMenu contextMenu)
        {
            try
            {
                var hwndSource = PresentationSource.FromVisual(contextMenu) as HwndSource;
                if (hwndSource != null)
                {
                    var hwnd = hwndSource.Handle;
                    SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch
            {
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                DeleteItem(item);
        }

        private void SetClipboardContent(ClipboardItem item, DataObject dataObject = null)
        {
            switch (item.Type)
            {
                case ClipboardType.Text:
                    SetClipboardData(dataObject, d => d.SetText(item.Content), () => ClipboardWin32Helper.SetText(item.Content));
                    break;
                case ClipboardType.RichText:
                    SetRichTextToClipboard(item, dataObject);
                    break;
                case ClipboardType.Image:
                    SetImageToClipboard(item, dataObject);
                    break;
                case ClipboardType.FileList:
                    SetFileListToClipboard(item, dataObject);
                    break;
            }
        }

        private static void SetClipboardData(DataObject dataObject, Action<DataObject> setDataAction, Action setClipboardAction)
        {
            if (dataObject != null)
                setDataAction(dataObject);
            else
                setClipboardAction();
        }

        private static void SetImageToClipboard(ClipboardItem item, DataObject dataObject)
        {
            if (string.IsNullOrEmpty(item.ImagePath))
                return;

            string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
            if (!System.IO.File.Exists(fullPath))
                return;

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(fullPath);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();

            SetClipboardData(dataObject, d => d.SetImage(image), () => ClipboardWin32Helper.SetImage(image));
        }

        private static void SetFileListToClipboard(ClipboardItem item, DataObject dataObject)
        {
            var stringCollection = new System.Collections.Specialized.StringCollection();
            stringCollection.AddRange(item.FilePaths.ToArray());

            if (dataObject != null)
            {
                dataObject.SetFileDropList(stringCollection);
            }
            else
            {
                var fileDataObject = new DataObject();
                fileDataObject.SetFileDropList(stringCollection);
                ClipboardWin32Helper.SetDataObject(fileDataObject);
            }
        }

        private static void SetRichTextToClipboard(ClipboardItem item, DataObject dataObject)
        {
            var target = dataObject ?? new DataObject();

            target.SetText(item.Content, TextDataFormat.Text);
            target.SetText(item.Content, TextDataFormat.UnicodeText);

            if (item.RichFormat == "HTML" && !string.IsNullOrEmpty(item.RichContent))
                target.SetText(item.RichContent, TextDataFormat.Html);
            else if (item.RichFormat == "RTF" && !string.IsNullOrEmpty(item.RichContent))
                target.SetText(item.RichContent, TextDataFormat.Rtf);

            if (!string.IsNullOrEmpty(item.CsvContent))
                target.SetText(item.CsvContent, TextDataFormat.CommaSeparatedValue);

            if (!string.IsNullOrEmpty(item.ImagePath))
            {
                string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                if (System.IO.File.Exists(fullPath))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(fullPath);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    target.SetImage(image);
                }
            }

            if (dataObject == null)
                ClipboardWin32Helper.SetDataObject(target);
        }

        private void PasteItemWithKeyboardModifiers(ClipboardItem item)
        {
            var (ctrl, _, shift, _) = KeyboardService.GetModifierKeysState();
            bool isCtrlShift = ctrl && shift;
            bool isCtrlOrShift = ctrl || shift;

            if (isCtrlShift && (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText))
            {
                PasteItemAsPlainTextRemoveNewlines(item);
            }
            else if (isCtrlOrShift && (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText))
            {
                PasteItemAsPlainText(item);
            }
            else
            {
                PasteItem(item);
            }
        }

        private void PasteItemAsPlainText(ClipboardItem item)
        {
            if (_isPasting) return;

            try
            {
                _isPasting = true;
                ClipboardWin32Helper.SetText(item.Content ?? "");
                PostPasteFlow(pasteHashText: item.Content ?? "", moveTopItem: item);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void PasteItemAsPlainTextRemoveNewlines(ClipboardItem item)
        {
            if (_isPasting) return;

            try
            {
                _isPasting = true;
                string text = (item.Content ?? "").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                while (text.Contains("  "))
                    text = text.Replace("  ", " ");

                text = text.Trim();
                ClipboardWin32Helper.SetText(text);
                PostPasteFlow(pasteHashText: text, moveTopItem: item);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        #region 主界面快捷键

        private bool TryMatchQuickPasteShortcut(ClipboardItem item, Models.QuickPasteMouseButton button, out Models.QuickPasteShortcut matched)
        {
            matched = null;
            if (_quickPasteShortcuts == null || _quickPasteShortcuts.Count == 0)
                return false;

            var (ctrl, alt, shift, _) = KeyboardService.GetModifierKeysState();

            foreach (var sc in _quickPasteShortcuts)
            {
                if (!sc.Enabled) continue;
                if (sc.MouseButton != button) continue;
                if (sc.Ctrl != ctrl || sc.Shift != shift || sc.Alt != alt) continue;
                if (!Models.QuickCommand.TypeIncludes(sc.ClipboardType, (int)item.Type)) continue;
                matched = sc;
                return true;
            }
            return false;
        }

        private void ExecuteQuickPasteAction(ClipboardItem item, Models.QuickPasteAction action, string quickCommandName = null)
        {

            ClipboardListBox.SelectedItem = item;
            switch (action)
            {
                case Models.QuickPasteAction.PasteAsPlainText:
                    PasteItemAsPlainText(item);
                    break;
                case Models.QuickPasteAction.PasteRemoveNewlines:
                    PasteItemAsPlainTextRemoveNewlines(item);
                    break;
                case Models.QuickPasteAction.PasteAndDelete:
                    PasteAndDeleteItem(item);
                    break;
                case Models.QuickPasteAction.Edit:
                    EditMenuItem_Click(null, null);
                    break;
                case Models.QuickPasteAction.Copy:
                    CopyMenuItem_Click(null, null);
                    break;
                case Models.QuickPasteAction.Delete:
                    DeleteItem(item);
                    break;
                case Models.QuickPasteAction.Split:
                    CharPicker_Click(null, null);
                    break;
                case Models.QuickPasteAction.OpenInBrowser:
                    OpenInBrowserMenuItem_Click(null, null);
                    break;
                case Models.QuickPasteAction.GenerateQRCode:
                    GenerateQRCode_Click(null, null);
                    break;
                case Models.QuickPasteAction.Group:
                    GroupMenuItem_Click(null, null);
                    break;
                case Models.QuickPasteAction.CustomRegex:

                    if (!string.IsNullOrEmpty(quickCommandName))
                    {
                        var qc = App.SettingsService?.Settings?.QuickCommands?
                            .FirstOrDefault(x => string.Equals(x.Name?.Trim(), quickCommandName.Trim(), StringComparison.Ordinal));
                        if (qc != null)
                        {
                            PasteWithCustomRegex(item, qc.Rules);
                            break;
                        }
                    }

                    PasteItemAsPlainTextRemoveNewlines(item);
                    break;
            }
        }

        public void ExecuteGlobalHotkeyAction(GlobalHotkey gh)
        {
            if (gh == null) return;

            int idx = gh.ItemIndex - 1;
            if (idx < 0) idx = 0;

            var items = _databaseService.GetItems(1, idx, null, null, null);
            if (items == null || items.Count == 0) return;

            var item = items[0];
            if (item == null) return;

            ExecuteQuickPasteAction(item, gh.Action, gh.QuickCommandName);
        }

        /// <summary>
        /// 顺序粘贴热键入口（全局热键触发，运行于 UI 线程）。上一次粘贴尚未完成时仅暂存一个待处理按键。
        /// </summary>
        public void OnSequentialPasteHotkey(bool fifo)
        {
            if (_seqPasteBusy)
            {
                _seqPasteHasPending = true;
                _seqPastePendingFifo = fifo;
                _seqPastePendingTime = DateTime.Now;
                return;
            }
            _ = RunSequentialPasteAsync(fifo, DateTime.Now);
        }

        private async Task RunSequentialPasteAsync(bool fifo, DateTime pressTime)
        {
            _seqPasteBusy = true;
            try
            {
                while (true)
                {
                    await ProcessSequentialPasteOnceAsync(fifo, pressTime);
                    if (!_seqPasteHasPending) break;
                    _seqPasteHasPending = false;
                    fifo = _seqPastePendingFifo;
                    pressTime = _seqPastePendingTime;
                }
            }
            finally
            {
                _seqPasteBusy = false;
            }
        }

        /// <summary>
        /// 顺序粘贴单次处理。数据始终基于数据库全量记录。
        /// fifo=true：先进先出，取最新一批记录（相邻记录间隔时间≤设定秒数视为同一批），按复制先后顺序依次粘贴；
        /// fifo=false：后进先出，从最新记录开始按列表顺序依次粘贴。
        /// 连续按下依次粘贴下一条；两次按下间隔超过设定秒数则重新基于数据库从头开始。
        /// 后进先出/先进先出的序列状态各自独立、互不影响。
        /// </summary>
        private async Task ProcessSequentialPasteOnceAsync(bool fifo, DateTime pressTime)
        {
            try
            {
                int intervalSeconds = Math.Max(1, App.SettingsService?.Settings?.SequentialPasteIntervalSeconds ?? 8);
                var now = pressTime;

                var queue = fifo ? _seqPasteQueueFifo : _seqPasteQueueLifo;
                var lastPress = fifo ? _seqPasteLastPressFifo : _seqPasteLastPressLifo;
                bool continueSequence = queue != null && queue.Count > 0 &&
                                        (now - lastPress).TotalSeconds <= intervalSeconds;

                if (fifo) _seqPasteLastPressFifo = now;
                else _seqPasteLastPressLifo = now;

                if (!continueSequence)
                {
                    // 新批次：始终基于数据库全量记录重建
                    var items = await Task.Run(() => _databaseService.GetItems(int.MaxValue, 0, null, null, null));
                    if (items == null || items.Count == 0) return;

                    if (fifo)
                    {
                        // 从最新记录向前收集同一批记录（相邻两条记录的时间间隔不超过设定秒数）
                        var batch = new List<ClipboardItem> { items[0] };
                        for (int i = 1; i < items.Count; i++)
                        {
                            var gap = (items[i - 1].CreatedAt - items[i].CreatedAt).TotalSeconds;
                            if (gap > intervalSeconds) break;
                            batch.Add(items[i]);
                        }
                        batch.Reverse(); // 批内按复制先后顺序（先进先出）
                        queue = batch;
                        _seqPasteQueueFifo = queue;
                        _seqPasteIndexFifo = 0;
                    }
                    else
                    {
                        queue = items;
                        _seqPasteQueueLifo = queue;
                        _seqPasteIndexLifo = 0;
                    }
                }
                else
                {
                    // 超出队列末尾后回绕到开头继续
                    if (fifo)
                    {
                        _seqPasteIndexFifo = (_seqPasteIndexFifo + 1) % _seqPasteQueueFifo.Count;
                    }
                    else
                    {
                        _seqPasteIndexLifo = (_seqPasteIndexLifo + 1) % _seqPasteQueueLifo.Count;
                    }
                }

                var index = fifo ? _seqPasteIndexFifo : _seqPasteIndexLifo;
                var item = queue[index];
                if (item == null) return;
                PasteItem(item);
            }
            catch
            {
                // 单次粘贴失败不中断热键处理
            }
        }

        private void PasteWithCustomRegex(ClipboardItem item,
            System.Collections.Generic.IEnumerable<Models.QuickCommandRule> rules)
        {
            if (_isPasting) return;
            if (item.Type != ClipboardType.Text && item.Type != ClipboardType.RichText) return;
            if (rules == null) return;

            try
            {
                _isPasting = true;
                string current = item.Content ?? "";

                int stepIndex = 0;
                foreach (var rawRule in rules)
                {
                    stepIndex++;
                    if (rawRule == null || !rawRule.Enabled) continue;

                    var rule = rawRule;
                    string fnName = rule.Function.ToString();
                    bool firstOnly = false;
                    string p1 = rule.Param1 ?? "";
                    string p2 = rule.Param2 ?? "";

                    try
                    {
                        current = Models.QuickCommand.ApplyRule(current, rule.Function, p1, p2, firstOnly);
                    }
                    catch (Exception ruleEx)
                    {
                        string err = string.Format(
                            Loc.Get("MainWindow.Message.QcRuleFailed", "快捷命令执行失败：规则 #{0} [{1}] 报错：{2}"),
                            stepIndex, fnName, ruleEx.Message);
                        AppMessageBox.Show(err, Loc.Get("Message.Error", "错误"),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                ClipboardWin32Helper.SetText(current ?? "");
                PostPasteFlow(pasteHashText: current ?? "", moveTopItem: item);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        #endregion

        private void PasteItem(ClipboardItem item)
        {
            if (_isPasting) return;

            try
            {
                _isPasting = true;

                SetClipboardContent(item);

                var hashText = (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                    ? item.Content ?? "" : null;
                var hashFiles = item.Type == ClipboardType.FileList && item.FilePaths != null
                    ? item.FilePaths : null;

                PostPasteFlow(pasteHashText: hashText, pasteHashFiles: hashFiles, moveTopItem: item);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void ActivateAndPaste(IntPtr windowHandle)
        {
            IntPtr targetHwnd = IntPtr.Zero;

            if (windowHandle != IntPtr.Zero)
            {
                targetHwnd = GetAncestor(windowHandle, GA_ROOT);
                if (targetHwnd == IntPtr.Zero)
                    targetHwnd = windowHandle;
            }
            else
            {
                targetHwnd = GetDesktopWindow();
            }

            if (targetHwnd == IntPtr.Zero || !IsWindow(targetHwnd)) return;

            Thread.Sleep(20);

            if (!TryBringWindowToForeground(targetHwnd))
            {
                return;
            }

            WaitForWindowActivated(targetHwnd);

            KeyboardService.WaitForModifiersReleased(500);

            var pasteMode = App.SettingsService?.GetPasteShortcutMode() ?? Services.PasteShortcutMode.Auto;
            KeyboardService.SimulatePaste(pasteMode);
        }

        private static void WaitForWindowActivated(IntPtr targetHwnd, int maxWaitMs = 200)
        {
            int waited = 0;
            while (waited < maxWaitMs)
            {
                IntPtr currentForeground = GetForegroundWindow();
                IntPtr currentRoot = GetAncestor(currentForeground, GA_ROOT);
                if (currentRoot == targetHwnd || currentForeground == targetHwnd)
                {
                    return;
                }
                Thread.Sleep(10);
                waited += 10;
            }

            Thread.Sleep(30);
        }

        private static bool TryBringWindowToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            IntPtr rootHwnd = GetAncestor(hWnd, GA_ROOT);
            if (rootHwnd != IntPtr.Zero)
            {
                hWnd = rootHwnd;
            }

            if (!IsWindow(hWnd) || !IsWindowVisible(hWnd)) return false;

            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
                uint currentThreadId = GetCurrentThreadId();

                bool attached = false;
                if (foregroundThreadId != currentThreadId && foregroundThreadId != 0)
                {
                    attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                try
                {
                    SetForegroundWindow(hWnd);
                    SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0,
                        (uint)(SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW));
                }
                finally
                {
                    if (attached)
                    {
                        AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                }

                if (IsForegroundWindow(hWnd)) return true;

                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                SetForegroundWindow(hWnd);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                if (IsForegroundWindow(hWnd)) return true;

                Thread.Sleep(30);
            }

            return false;
        }

        private static bool IsForegroundWindow(IntPtr hWnd)
        {
            IntPtr foreground = GetForegroundWindow();
            IntPtr root = GetAncestor(foreground, GA_ROOT);
            return root == hWnd || foreground == hWnd;
        }

        private void PostPasteFlow(string pasteHashText = null, List<string> pasteHashFiles = null,
            ClipboardItem moveTopItem = null, List<ClipboardItem> moveTopItems = null,
            IntPtr? overrideTargetHwnd = null)
        {
            var clipboardMonitor = App.GetClipboardMonitor();
            clipboardMonitor?.MarkPasteOperation();
            clipboardMonitor?.IgnoreNextChange(1000);

            if (pasteHashText != null)
                clipboardMonitor?.SetLastPasteHashText(pasteHashText);
            if (pasteHashFiles != null)
                clipboardMonitor?.SetLastPasteHashFiles(pasteHashFiles);

            if (moveTopItem != null && App.SettingsService.Settings.MoveToTopAfterPaste && !moveTopItem.GroupId.HasValue)
            {
                _databaseService.UpdateItemTimestampById(moveTopItem.Id);
                _viewModel.LoadItems();
            }
            else if (moveTopItems != null && App.SettingsService.Settings.MoveToTopAfterPaste)
            {
                foreach (var item in moveTopItems)
                {
                    if (!item.GroupId.HasValue)
                        _databaseService.UpdateItemTimestampById(item.Id);
                }
                _viewModel.LoadItems();
            }

            IntPtr targetHwnd = IntPtr.Zero;
            var focusService = App.GetFocusService();

            if (overrideTargetHwnd.HasValue && overrideTargetHwnd.Value != IntPtr.Zero)
            {
                targetHwnd = overrideTargetHwnd.Value;
            }
            else if (_capturedTargetHwnd != IntPtr.Zero)
            {
                targetHwnd = _capturedTargetHwnd;
            }
            else
            {
                targetHwnd = focusService?.GetLiveForegroundTarget() ?? IntPtr.Zero;
                if (targetHwnd == IntPtr.Zero)
                    targetHwnd = _lastFocusHwndWhenShow;
                if (targetHwnd == IntPtr.Zero && focusService != null)
                {
                    targetHwnd = focusService.LastFocusHwnd;
                    if (targetHwnd == IntPtr.Zero)
                    {
                        targetHwnd = focusService.GetPreviousFocusHwnd(1);
                    }
                }
            }
            _capturedTargetHwnd = IntPtr.Zero;

            var windowStateService = App.GetWindowStateService();
            bool shouldHide = _isVisible && windowStateService != null && !windowStateService.IsPinned;

            if (shouldHide)
            {
                HideWindow();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActivateAndPaste(targetHwnd);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static IntPtr GetDesktopWindow()
        {

            var hwnd = FindWindow("Progman", null);
            if (hwnd == IntPtr.Zero)
            {
                hwnd = FindWindow("WorkerW", null);
            }
            return hwnd;
        }

        private void CopyToClipboard(ClipboardItem item)
        {
            try
            {
                SetClipboardContent(item);

                var clipboardMonitor = App.GetClipboardMonitor();
                clipboardMonitor?.IgnoreNextChange(1000);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.CopyFailed", "复制失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteItem(ClipboardItem item)
        {
            _databaseService.DeleteItem(item.Id);
            _viewModel.RemoveItem(item);
        }

        private void PasteTextWithHwnd(string text, IntPtr targetHwnd)
        {
            if (_isPasting) return;

            try
            {
                _isPasting = true;
                ClipboardWin32Helper.SetText(text);
                PostPasteFlow(pasteHashText: text, overrideTargetHwnd: targetHwnd);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void MultiSelectToggleButton_Click(object sender, RoutedEventArgs e)
            => SetMultiSelectMode(!_viewModel.IsMultiSelectMode);

        private bool _wasPinnedBeforeMultiSelect = false;

        private void SetMultiSelectMode(bool enabled)
        {
            _viewModel.IsMultiSelectMode = enabled;
            _viewModel.ClearSelection();

            _capturedTargetHwnd = IntPtr.Zero;

            if (enabled)
            {
                _wasPinnedBeforeMultiSelect = _viewModel.IsPinned;
                if (!_viewModel.IsPinned)
                {
                    _viewModel.IsPinned = true;
                    Topmost = true;
                }
            }
            else
            {
                if (!_wasPinnedBeforeMultiSelect)
                {
                    _viewModel.IsPinned = false;
                    Topmost = false;
                }
            }
        }

        private void MultiSelectCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is ClipboardItem item)
            {
                _viewModel.ToggleSelection(item);
                e.Handled = true;
            }
        }

        private void BatchPaste_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItemsOrShowMessage(Loc.Get("MainWindow.Message.SelectItemsToPaste", "请先选择要粘贴的项"));
            if (selectedItems == null) return;

            if (_isPasting)
                return;

            var textItems = selectedItems.Where(i => i.Type == ClipboardType.Text || i.Type == ClipboardType.RichText).ToList();
            var imageItems = selectedItems.Where(i => i.Type == ClipboardType.Image).ToList();
            var fileItems = selectedItems.Where(i => i.Type == ClipboardType.FileList).ToList();

            if (textItems.Count > 0)
            {
                BatchPasteTextItems(textItems);
            }
            else if (imageItems.Count > 0)
            {
                BatchPasteItemsSequentially(imageItems);
            }
            else if (fileItems.Count > 0)
            {
                BatchPasteFileItems(fileItems);
            }
        }

        private void BatchPasteTextItems(List<ClipboardItem> textItems)
        {
            try
            {
                _isPasting = true;

                var combinedText = string.Join("\n", textItems.Select(i => i.Content ?? ""));
                ClipboardWin32Helper.SetText(combinedText);
                PostPasteFlow(pasteHashText: combinedText, moveTopItems: textItems);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void BatchPasteFileItems(List<ClipboardItem> fileItems)
        {
            try
            {
                _isPasting = true;

                var allFilePaths = new List<string>();
                foreach (var item in fileItems)
                {
                    if (item.FilePaths != null)
                        allFilePaths.AddRange(item.FilePaths);
                }

                if (allFilePaths.Count == 0)
                {
                    _isPasting = false;
                    return;
                }

                var fileCollection = new System.Collections.Specialized.StringCollection();
                fileCollection.AddRange(allFilePaths.ToArray());
                ClipboardWin32Helper.SetFileDropList(fileCollection);
                PostPasteFlow(pasteHashFiles: allFilePaths, moveTopItems: fileItems);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private async void BatchPasteItemsSequentially(List<ClipboardItem> items)
        {
            if (_isPasting) return;

            var focusService = App.GetFocusService();
            var targetHwnd = focusService?.LastFocusHwnd ?? IntPtr.Zero;

            var windowStateService = App.GetWindowStateService();
            if (windowStateService != null && !windowStateService.IsPinned)
            {
                HideWindow();
                await System.Threading.Tasks.Task.Delay(50);
            }

            _isPasting = true;
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    SetClipboardContent(item);

                    var clipboardMonitor = App.GetClipboardMonitor();
                    clipboardMonitor?.MarkPasteOperation();
                    clipboardMonitor?.IgnoreNextChange(1000);

                    if (App.SettingsService.Settings.MoveToTopAfterPaste && !item.GroupId.HasValue)
                        _databaseService.UpdateItemTimestampById(item.Id);

                    ActivateAndPaste(targetHwnd);

                    if (i < items.Count - 1)
                        await System.Threading.Tasks.Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void BatchDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItemsOrShowMessage(Loc.Get("MainWindow.Message.SelectItemsToDelete", "请先选择要删除的项"));
            if (selectedItems == null) return;

            if (AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.ConfirmDeleteSelected", "确定要删除选中的 {0} 项吗？"), selectedItems.Count), 
                Loc.Get("Message.Confirm", "确认"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                    _databaseService.DeleteItem(item.Id);

                _viewModel.ClearSelection();
                _viewModel.LoadItems();
            }
        }

        private void BatchGroup_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItemsOrShowMessage(Loc.Get("MainWindow.Message.SelectItemsToGroup", "请先选择要分组的项"));
            if (selectedItems == null) return;

            var menu = CreateGroupMenu(null, null, groupId =>
            {
                foreach (var item in selectedItems)
                    _databaseService.UpdateItemGroup(item.Id, groupId);

                _viewModel.ClearSelection();
                _viewModel.LoadItems();
            });
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (AppMessageBox.Show(Loc.Get("MainWindow.Message.ConfirmDeleteAll", "是否删除本页面'全部记录'？"),
                Loc.Get("Message.Confirm", "确认"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                bool isLink = _viewModel.TypeFilter == FilterType.Link;
                int deleted = _databaseService.DeleteItemsByFilter(_viewModel.GroupFilter, isLink ? (int?)null : _viewModel.TypeFilter, _viewModel.SearchText, ungroupedOnly: false, linkOnly: isLink);
                _viewModel.ClearSelection();
                _viewModel.LoadItems();
                AppMessageBox.Show(string.Format(Loc.Get("Message.DeletedCount", "已删除 {0} 条记录"), deleted),
                    Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.DeleteFailed", "删除失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDuplicates_Click(object sender, RoutedEventArgs e)
        {
            if (AppMessageBox.Show(Loc.Get("Settings.History.DeleteDuplicatesConfirm", "确定要删除较早的重复记录吗？\n\n每组重复将保留最新一条，已分组的记录将保留。"),
                Loc.Get("Message.Confirm", "确认"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                int deleted = _databaseService.DeleteOldDuplicates();
                _viewModel.ClearSelection();
                _viewModel.LoadItems();
                AppMessageBox.Show(
                    string.Format(Loc.Get("Settings.History.DeleteDuplicatesResult", "已删除 {0} 条重复记录"), deleted),
                    Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.DeleteFailed", "删除失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAllUngrouped_Click(object sender, RoutedEventArgs e)
        {
            if (AppMessageBox.Show(Loc.Get("MainWindow.Message.ConfirmDeleteAllUngrouped", "是否删除本页面'所有未分组的记录'？"),
                Loc.Get("Message.Confirm", "确认"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                bool isLink = _viewModel.TypeFilter == FilterType.Link;
                int deleted = _databaseService.DeleteItemsByFilter(null, isLink ? (int?)null : _viewModel.TypeFilter, _viewModel.SearchText, ungroupedOnly: true, linkOnly: isLink);
                _viewModel.ClearSelection();
                _viewModel.LoadItems();
                AppMessageBox.Show(string.Format(Loc.Get("Message.DeletedCount", "已删除 {0} 条记录"), deleted),
                    Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.DeleteFailed", "删除失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteInvalid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var invalidItems = await Task.Run(() =>
                {
                    var allFileItems = _databaseService.GetItems(int.MaxValue, 0, null, (int)ClipboardType.FileList, null);
                    var allImageItems = _databaseService.GetItems(int.MaxValue, 0, null, (int)ClipboardType.Image, null);
                    return allFileItems
                        .Concat(allImageItems)
                        .Where(FileValidationService.IsItemInvalid)
                        .ToList();
                });

                if (invalidItems.Count == 0)
                {
                    AppMessageBox.Show(Loc.Get("MainWindow.Message.NoInvalid", "没有需要清理的无效记录"),
                        Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int deleted = await Task.Run(() => _databaseService.DeleteItemsByIds(invalidItems.Select(i => i.Id)));
                _viewModel.LoadItems();

                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.DeletedInvalid", "已删除 {0} 条无效记录"), deleted),
                    Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show(string.Format(Loc.Get("MainWindow.Message.DeleteFailed", "删除失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ClipboardItem> GetSelectedItemsOrShowMessage(string emptyMessage)
        {
            var selectedItems = _viewModel.GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                AppMessageBox.Show(emptyMessage, Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            return selectedItems;
        }

        private void ItemGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _dragStartPoint = e.GetPosition(this);
                return;
            }

            if (_isDragStarted || sender is not FrameworkElement fe)
                return;

            var diff = _dragStartPoint - e.GetPosition(this);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (fe.DataContext is ClipboardItem item)
                {
                    _isDragStarted = true;
                    try
                    {
                        StartDrag(item);
                    }
                    finally
                    {
                        _isDragStarted = false;
                    }
                }
            }
        }

        private void StartDrag(ClipboardItem item)
        {
            List<ClipboardItem> items;
            if (_viewModel.IsMultiSelectMode)
            {
                items = _viewModel.GetSelectedItems();

                if (!items.Contains(item))
                    items = new List<ClipboardItem> { item };
            }
            else
            {
                items = new List<ClipboardItem> { item };
            }

            if (items.Count == 0)
                return;

            var data = BuildDragDataObject(items);
            if (data == null)
                return;

            DragDrop.DoDragDrop(ClipboardListBox, data, DragDropEffects.Copy);
        }

        private DataObject BuildDragDataObject(List<ClipboardItem> items)
        {
            if (items.Count == 1)
            {
                var data = new DataObject();
                SetClipboardContent(items[0], data);

                var localFiles = TryGetItemLocalFiles(items[0]);
                if (localFiles.Count > 0)
                {
                    var fileCollection = new System.Collections.Specialized.StringCollection();
                    fileCollection.AddRange(localFiles.ToArray());
                    data.SetFileDropList(fileCollection);
                }

                return data.GetFormats().Length > 0 ? data : null;
            }

            var merged = new DataObject();
            var allFiles = new List<string>();
            var textParts = new List<string>();

            foreach (var it in items)
            {
                var files = TryGetItemLocalFiles(it);
                if (files.Count > 0)
                    allFiles.AddRange(files);

                if ((it.Type == ClipboardType.Text || it.Type == ClipboardType.RichText) &&
                    !string.IsNullOrEmpty(it.Content))
                {
                    textParts.Add(it.Content);
                }
            }

            if (allFiles.Count > 0)
            {
                var fileCollection = new System.Collections.Specialized.StringCollection();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in allFiles)
                {
                    if (seen.Add(path))
                        fileCollection.Add(path);
                }
                if (fileCollection.Count > 0)
                    merged.SetFileDropList(fileCollection);
            }

            if (textParts.Count > 0)
            {
                string combined = string.Join(Environment.NewLine, textParts);
                merged.SetText(combined, TextDataFormat.UnicodeText);
                merged.SetText(combined, TextDataFormat.Text);
            }

            return merged.GetFormats().Length > 0 ? merged : null;
        }

        private static List<string> TryGetItemLocalFiles(ClipboardItem item)
        {
            var files = new List<string>();
            try
            {
                if (item.Type == ClipboardType.FileList && item.FilePaths?.Count > 0)
                {
                    foreach (var path in item.FilePaths)
                    {
                        if (File.Exists(path))
                            files.Add(path);
                    }
                }
                else if (item.Type == ClipboardType.Image && !string.IsNullOrEmpty(item.ImagePath))
                {
                    string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                    if (File.Exists(fullPath))
                        files.Add(fullPath);
                }
                else if ((item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText) && item.IsLocalPath)
                {
                    string path = TrimQuotes(item.Content?.Trim() ?? "");
                    if (File.Exists(path))
                        files.Add(path);
                }
            }
            catch
            {

            }
            return files;
        }

        private void ItemBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is ClipboardItem item)
            {
                _currentTooltipItem = item;
                _currentTooltipBorder = border;

                if (_tooltipTimer == null)
                {
                    _tooltipTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _tooltipTimer.Tick += OnTooltipTimerTick;
                }
                _tooltipTimer.Stop();
                _tooltipTimer.Start();
            }
        }

        private void OnTooltipTimerTick(object sender, EventArgs e)
        {
            _tooltipTimer.Stop();
            if (_currentTooltipItem != null && _currentTooltipBorder != null && _currentTooltipBorder.IsMouseOver)
            {
                ShowCustomTooltip(_currentTooltipBorder, _currentTooltipItem);
            }
        }

        private void ItemBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            _tooltipTimer?.Stop();
            _currentTooltipItem = null;
            _currentTooltipBorder = null;
            HideCustomTooltip();
        }

        private ToolTip _customTooltip;

        private void ShowCustomTooltip(Border border, ClipboardItem item)
        {
            HideCustomTooltip();

            _customTooltip = new ToolTip
            {
                MaxWidth = 400,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                Background = (System.Windows.Media.Brush)FindResource("WindowBackground"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextForeground"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Content = item,
                ContentTemplateSelector = (DataTemplateSelector)FindResource("PreviewTemplateSelector")
            };

            _customTooltip.IsOpen = true;
        }

        private void HideCustomTooltip()
        {
            if (_customTooltip != null)
            {
                _customTooltip.IsOpen = false;
                _customTooltip = null;
            }
        }

        private void PanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.SettingsService.Settings.UseSystemCharPanel)
            {

                HideWindow();
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    System.Threading.Thread.Sleep(100);
                    Dispatcher.BeginInvoke(new Action(() => KeyboardService.SendWinPeriod()));
                });
                return;
            }

            if (_viewModel.IsFilterPanelVisible)
            {
                _viewModel.IsFilterPanelVisible = false;
                _viewModel.TypeFilter = null;
            }
            _panelState = (_panelState + 1) % 2;
            UpdatePanelState();
        }

        private void UpdatePanelState()
        {
            switch (_panelState)
            {
                case 0:
                    ClipboardPanel.Visibility = Visibility.Visible;
                    CharPanel.Visibility = Visibility.Collapsed;
                    break;
                case 1:
                    ClipboardPanel.Visibility = Visibility.Collapsed;
                    CharPanel.Visibility = Visibility.Visible;
                    LoadCharacterData();
                    break;
            }
            OnPropertyChanged(nameof(IsPanelActive));
        }

        private void LoadCharacterData()
        {
            if (_charPanelBuilt) return;
            _charPanelBuilt = true;
            BuildPanel(_charGroups, CharTabControl, CharContentPanel, "CharPanel");
        }

        private List<CharGroupData> LoadPanelDataRaw(string subDir, string fileName, string embeddedResourceName)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", subDir);
                var path = Path.Combine(dir, fileName);
                string json;

                if (File.Exists(path))
                {
                    json = File.ReadAllText(path, Encoding.UTF8);
                }
                else
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using var stream = assembly.GetManifestResourceStream(embeddedResourceName);
                    if (stream == null) return null;
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    json = reader.ReadToEnd();
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(path, json, Encoding.UTF8);
                }

                var data = System.Text.Json.JsonSerializer.Deserialize(json, Services.AppJsonContext.Default.CharDataRoot);
                return data?.Groups;
            }
            catch
            {
                return null;
            }
        }

        private void BuildPanel(List<CharGroupData> groups, TabControl tabControl, StackPanel contentPanel,
            string locPrefix)
        {
            tabControl.Items.Clear();
            contentPanel.Children.Clear();

            var isZh = Loc.CurrentLanguageCode.StartsWith("zh");

            foreach (var group in groups)
            {
                var groupName = isZh ? group.NameZh : group.NameEn;
                var locKey = $"{locPrefix}.Group.{GroupIdToLocalKey(group.Id)}";
                var displayName = Loc.Get(locKey, groupName);

                var tabItem = new TabItem
                {
                    Header = new TextBlock
                    {
                        Text = displayName,
                        FontSize = 10,
                        TextWrapping = TextWrapping.NoWrap,
                        TextAlignment = TextAlignment.Center
                    },
                    Tag = group.Id
                };
                tabControl.Items.Add(tabItem);
            }

            if (tabControl.Items.Count > 0)
                tabControl.SelectedIndex = 0;
        }

        private void LoadGroupContent(CharGroupData group, StackPanel contentPanel, string locPrefix)
        {
            contentPanel.Children.Clear();

            var isZh = Loc.CurrentLanguageCode.StartsWith("zh");
            var groupName = isZh ? group.NameZh : group.NameEn;
            var locKey = $"{locPrefix}.Group.{GroupIdToLocalKey(group.Id)}";
            var displayName = Loc.Get(locKey, groupName);

            var groupBorder = new Border
            {
                Tag = group.Id,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var groupStack = new StackPanel();

            var headerText = new TextBlock
            {
                Text = displayName,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextForeground"),
                Margin = new Thickness(4, 4, 4, 4)
            };
            groupStack.Children.Add(headerText);

            var wrapPanel = new WrapPanel
            {
                Margin = new Thickness(2, 0, 2, 0)
            };

            foreach (var ch in group.Chars)
            {
                var btn = new Button
                {
                    Tag = ch,
                    Cursor = Cursors.Hand,
                    Style = (Style)FindResource("CharButtonStyle"),
                    Margin = new Thickness(1),
                    Content = ch,
                    Width = 32,
                    Height = 32,
                    FontSize = 14,
                    ToolTip = new TextBlock { Text = ch, FontSize = 42, TextAlignment = TextAlignment.Center }
                };

                btn.Click += CharButton_Click;
                wrapPanel.Children.Add(btn);
            }

            groupStack.Children.Add(wrapPanel);
            groupBorder.Child = groupStack;
            contentPanel.Children.Add(groupBorder);
        }

        private void RefreshPanelLocalization(List<CharGroupData> groups, TabControl tabControl, string locPrefix)
        {
            if (groups == null || groups.Count == 0) return;

            var isZh = Loc.CurrentLanguageCode.StartsWith("zh");

            foreach (TabItem tabItem in tabControl.Items)
            {
                var groupId = tabItem.Tag as string;
                var group = groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null) continue;

                var locKey = $"{locPrefix}.Group.{GroupIdToLocalKey(group.Id)}";
                var displayName = Loc.Get(locKey, isZh ? group.NameZh : group.NameEn);
                if (tabItem.Header is TextBlock tb)
                    tb.Text = displayName;
            }
        }

        private void PanelTabControl_SelectionChanged(TabControl tabControl, List<CharGroupData> groups,
            StackPanel contentPanel, string locPrefix)
        {
            if (tabControl.SelectedItem is TabItem tabItem && tabItem.Tag is string groupId)
            {
                var group = groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    LoadGroupContent(group, contentPanel, locPrefix);
                }
            }
        }

        private static string GroupIdToLocalKey(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var parts = id.Split('_');
            var result = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                result.Append(char.ToUpper(part[0]));
                result.Append(part.Substring(1));
            }
            return result.ToString();
        }

        private void CharButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string ch)
            {
                if (_isPasting) return;
                try
                {
                    _isPasting = true;
                    ClipboardWin32Helper.SetText(ch);
                    PostPasteFlow(pasteHashText: ch);
                }
                catch
                {
                }
                finally
                {
                    _isPasting = false;
                }
            }
        }

        private void CharTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PanelTabControl_SelectionChanged(CharTabControl, _charGroups, CharContentPanel, "CharPanel");
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                var found = FindVisualChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
