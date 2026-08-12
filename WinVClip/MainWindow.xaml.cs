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
    // ScrollViewer 动画滚动辅助类
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

    // 转换器基类，提供通用的 ProvideValue 和 ConvertBack 实现
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

    public class ItemTypeToUrlVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ClipboardItem item)
                return Visibility.Collapsed;

            if (item.Type != ClipboardType.Text && item.Type != ClipboardType.RichText)
                return Visibility.Collapsed;

            return Visibility.Visible;
        }
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

    public class TypeFilterToBackgroundConverter : BaseConverter
    {
        private static readonly SolidColorBrush ActiveFilterBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(parameter?.ToString(), out int targetTypeValue))
            {
                int? currentFilter = value as int?;
                bool isActive = targetTypeValue switch
                {
                    -1 => !currentFilter.HasValue,
                    0 => currentFilter == (int)ClipboardType.Text,
                    1 => currentFilter == (int)ClipboardType.Image,
                    2 => currentFilter == (int)ClipboardType.FileList,
                    3 => currentFilter == FilterType.Link,
                    _ => false
                };
                
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
                bool isActive = targetTypeValue switch
                {
                    -1 => !currentFilter.HasValue,
                    0 => currentFilter == (int)ClipboardType.Text,
                    1 => currentFilter == (int)ClipboardType.Image,
                    2 => currentFilter == (int)ClipboardType.FileList,
                    3 => currentFilter == FilterType.Link,
                    _ => false
                };
                
                if (isActive)
                    return System.Windows.Media.Brushes.White;
            }
            return Application.Current.FindResource("TextForeground");
        }
    }

    public class GroupTab
    {
        public long GroupId { get; set; }
        public string DisplayName { get; set; } = "";
        public string ToolTip { get; set; } = "";

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

        private List<ClipboardItem> _filteredLinkItems = new List<ClipboardItem>();

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
                _searchDelayTimer?.Stop();
                _searchDelayTimer?.Start();
            }
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
                GroupTabs.Add(new GroupTab { GroupId = group.Id, DisplayName = displayName, ToolTip = group.Name });
            }
            GroupTabs.Add(new GroupTab { GroupId = -1, DisplayName = "➕", ToolTip = Loc.Get("MainWindow.GroupManage", "分组管理") });
            SelectedGroupTabIndex = 0;
        }

        public bool HasItems => ClipboardItems.Count > 0;

        public bool IsEmpty => ClipboardItems.Count == 0;

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private int _textCount;
        public int TextCount
        {
            get => _textCount;
            set
            {
                _textCount = value;
                OnPropertyChanged();
            }
        }

        private int _imageCount;
        public int ImageCount
        {
            get => _imageCount;
            set
            {
                _imageCount = value;
                OnPropertyChanged();
            }
        }

        private int _fileCount;
        public int FileCount
        {
            get => _fileCount;
            set
            {
                _fileCount = value;
                OnPropertyChanged();
            }
        }

        private int _richTextCount;
        public int RichTextCount
        {
            get => _richTextCount;
            set
            {
                _richTextCount = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get
            {
                if (IsAnyFilterActive || !string.IsNullOrEmpty(SearchText))
                {
                    return Loc.Get("MainWindow.Status.Filtered", _totalCount, _grandTotalCount);
                }
                return Loc.Get("MainWindow.Status.Total", _totalCount);
            }
        }

        private int _grandTotalCount;
        public int GrandTotalCount
        {
            get => _grandTotalCount;
            set
            {
                _grandTotalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private bool _isWindowVisible = false;
        public bool IsWindowVisible
        {
            get => _isWindowVisible;
            set => _isWindowVisible = value;
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
            _filteredLinkItems.Clear();
            try
            {
                List<ClipboardItem> newItems = null;
                Dictionary<int, int> typeCounts = null;
                int totalCount = 0;
                int grandTotalCount = 0;
                int linkFilterCount = 0;
                await Task.Run(() =>
                {
                    if (TypeFilter == FilterType.Link)
                {
                    var allTextItems = _databaseService.GetItems(int.MaxValue, 0, SearchText,
                        (int)ClipboardType.Text, GroupFilter);
                    _filteredLinkItems = allTextItems?.Where(item => MainWindow.IsValidUrl(item.Content)).ToList() ?? new List<ClipboardItem>();
                    newItems = _filteredLinkItems.Take(PageSize).ToList();
                    linkFilterCount = _filteredLinkItems.Count;
                }
                    else
                    {
                        newItems = _databaseService.GetItems(PageSize, _currentOffset, SearchText,
                            TypeFilter, GroupFilter);
                    }
                    totalCount = _databaseService.GetTotalCount(SearchText, TypeFilter, GroupFilter);
                    grandTotalCount = _databaseService.GetTotalCount(null, null, null);
                    typeCounts = _databaseService.GetItemCountsByType(null, null);
                });
                
                if (newItems == null)
                    return;

                _currentOffset = newItems.Count;
                HasMoreItems = newItems.Count >= PageSize;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 空列表：走 AddRange（批量 Add 通知），避免 ReplaceAll 触发的
                    // Reset 通知 → 容器重建 → 闪烁。
                    // 非空列表（筛选/搜索变化）：用 ReplaceAll，确保旧项被清除。
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

                    if (TypeFilter == FilterType.Link)
                    {
                        TotalCount = linkFilterCount;
                    }
                    else
                    {
                        TotalCount = totalCount;
                    }
                    GrandTotalCount = grandTotalCount;
                    if (typeCounts != null)
                    {
                        TextCount = typeCounts.ContainsKey((int)ClipboardType.Text) ? typeCounts[(int)ClipboardType.Text] : 0;
                        ImageCount = typeCounts.ContainsKey((int)ClipboardType.Image) ? typeCounts[(int)ClipboardType.Image] : 0;
                        FileCount = typeCounts.ContainsKey((int)ClipboardType.FileList) ? typeCounts[(int)ClipboardType.FileList] : 0;
                        RichTextCount = typeCounts.ContainsKey((int)ClipboardType.RichText) ? typeCounts[(int)ClipboardType.RichText] : 0;
                    }
                });
            }
            catch (Exception)
            {
                HasMoreItems = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadMoreItemsAsync()
        {
            if (IsLoadingMore || !HasMoreItems)
                return;

            IsLoadingMore = true;
            try
            {
                List<ClipboardItem> newItems = null;
                await Task.Run(() =>
                {
                    if (TypeFilter == FilterType.Link)
                    {
                        newItems = _filteredLinkItems.Skip(_currentOffset).Take(PageSize).ToList();
                    }
                    else
                    {
                        newItems = _databaseService.GetItems(PageSize, _currentOffset, SearchText,
                            TypeFilter, GroupFilter);
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
            _currentOffset = 0;
            HasMoreItems = true;
            SelectedItemIds.Clear();
        }

        /// <summary>隐藏时的内存修剪（非破坏性）——只清图片等大对象，保留文本数据缓存。
        /// 下次显示窗口时直接复用缓存，避免重新加载引发的列表闪烁。</summary>
        public void TrimLargeObjects()
        {
            foreach (var item in ClipboardItems)
            {
                item.TrimLargeObjects();
            }
        }

        /// <summary>当前列表中是否已有可复用的缓存数据（即隐藏时未清空）。</summary>
        public bool HasCachedItems => ClipboardItems.Count > 0 || _currentOffset != 0;

        /// <summary>检查缓存是否仍然有效（筛选未变、记录总数未变），无效则需重新加载。
        /// 隐藏窗口时我们保留了轻量文本数据缓存，显示时借此判断能否直接复用。</summary>
        public bool IsCacheValid()
        {
            if (!HasCachedItems) return false;
            // 有搜索关键字或筛选：不能依赖缓存（捕获的新项可能正好匹配或不匹配筛选）
            if (!string.IsNullOrWhiteSpace(SearchText)) return false;
            if (TypeFilter != null) return false;
            if (GroupFilter != null) return false;
            try
            {
                // 快速查询数据库总数，避免重新加载整个列表
                int actualTotal = _databaseService.GetTotalCount(null, null, null);
                return actualTotal == GrandTotalCount;
            }
            catch
            {
                return false;
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
        }

        public void ClearSelection()
        {
            foreach (var item in ClipboardItems.Where(i => i.IsSelected))
            {
                item.IsSelected = false;
            }
            SelectedItemIds.Clear();
            OnPropertyChanged(nameof(SelectedItemIds));
        }

        public List<ClipboardItem> GetSelectedItems()
        {
            return ClipboardItems.Where(item => SelectedItemIds.Contains(item.Id)).ToList();
        }

        public void AddItem(ClipboardItem item)
        {
            // 窗口隐藏时也同步写入集合与计数：保证下次 Show 时 ListBox 直接已是最新，
            // 不需要等用户点过滤/分组才触发 LoadItems。集合操作很轻且有 200 条上限，
            // 与「隐藏时保留文本缓存、Show 时复用」的抗闪烁策略兼容。
            ClipboardItems.Insert(0, item);
            if (ClipboardItems.Count > 200)
            {
                ClipboardItems.RemoveAt(ClipboardItems.Count - 1);
            }
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));

            TotalCount++;
            GrandTotalCount++;
            switch (item.Type)
            {
                case ClipboardType.Text:
                    TextCount++;
                    break;
                case ClipboardType.Image:
                    ImageCount++;
                    break;
                case ClipboardType.FileList:
                    FileCount++;
                    break;
                case ClipboardType.RichText:
                    RichTextCount++;
                    TextCount++;
                    break;
            }
            OnPropertyChanged(nameof(StatusText));

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
            switch (item.Type)
            {
                case ClipboardType.Text:
                    if (TextCount > 0) TextCount--;
                    break;
                case ClipboardType.Image:
                    if (ImageCount > 0) ImageCount--;
                    break;
                case ClipboardType.FileList:
                    if (FileCount > 0) FileCount--;
                    break;
                case ClipboardType.RichText:
                    if (RichTextCount > 0) RichTextCount--;
                    if (TextCount > 0) TextCount--;
                    break;
            }
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private bool _isVisible = false;
        private HwndSource _hwndSource;
        private double _savedLeft;
        private double _savedTop;
        private System.Windows.Point _dragStartPoint;

        private System.Windows.Threading.DispatcherTimer _tooltipTimer;
        private ClipboardItem _currentTooltipItem;
        private Border _currentTooltipBorder;
        private bool _isPasting = false;
        private IntPtr _lastFocusHwndWhenShow = IntPtr.Zero;
        private IntPtr _capturedTargetHwnd = IntPtr.Zero;
        private List<Models.QuickPasteShortcut> _quickPasteShortcuts = new List<Models.QuickPasteShortcut>();

        private System.Threading.Timer _backgroundGcTimer;

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

            _savedLeft = Left;
            _savedTop = Top;
            
            SourceInitialized += MainWindow_SourceInitialized;
            _settingsService.SettingsChanged += OnSettingsChanged;
            _viewModel.ItemAdded += OnItemAdded;
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            LoadQuickPasteShortcuts();
        }

        /// <summary>从设置加载主界面快捷键配置到内存缓存。</summary>
        private void LoadQuickPasteShortcuts()
        {
            _quickPasteShortcuts = _settingsService.Settings.QuickPasteShortcuts?
                .Select(s => s.Clone()).ToList() ?? new List<Models.QuickPasteShortcut>();
        }

        private void ApplyLocalization()
        {
            FilterToggleButton.ToolTip = Loc.Get("MainWindow.ToolTip.Filter", "筛选");
            SettingsButton.ToolTip = Loc.Get("MainWindow.ToolTip.Settings", "设置");
            PinButton.ToolTip = Loc.Get("MainWindow.ToolTip.Pin", "置顶");
            CloseButton.ToolTip = Loc.Get("MainWindow.ToolTip.Close", "关闭 (ESC)");
            
            FilterAllButton.ToolTip = Loc.Get("MainWindow.Filter.All", "全部");
            FilterTextButton.ToolTip = Loc.Get("MainWindow.Filter.Text", "文本");
            FilterLinkButton.ToolTip = Loc.Get("MainWindow.Filter.Link", "链接");
            FilterImageButton.ToolTip = Loc.Get("MainWindow.Filter.Image", "图片");
            FilterFileButton.ToolTip = Loc.Get("MainWindow.Filter.File", "文件");
            
            MenuPasteWithFormat.Header = Loc.Get("MainWindow.ContextMenu.PasteWithFormat", "带格式粘贴");
            MenuPasteAsText.Header = Loc.Get("MainWindow.ContextMenu.PasteAsText", "纯文本粘贴");
            MenuPasteRemoveNewlines.Header = Loc.Get("MainWindow.ContextMenu.PasteRemoveNewlines", "纯文本去换行粘贴");
            MenuPasteAsImage.Header = Loc.Get("MainWindow.ContextMenu.PasteAsImage", "图片粘贴");
            MenuImagePaste.Header = Loc.Get("MainWindow.ContextMenu.ImagePaste", "图片粘贴");
            MenuImageFilePaste.Header = Loc.Get("MainWindow.ContextMenu.ImageFilePaste", "图片文件粘贴");
            MenuOpenInBrowser.Header = Loc.Get("MainWindow.ContextMenu.OpenInBrowser", "在浏览器打开");
            MenuOpenFolder.Header = Loc.Get("MainWindow.ContextMenu.OpenFolder", "打开文件夹");
            MenuEdit.Header = Loc.Get("MainWindow.ContextMenu.Edit", "编辑");
            MenuQuickCommands.Header = Loc.Get("MainWindow.ContextMenu.QuickCommands", "快捷命令");
            MenuCopy.Header = Loc.Get("MainWindow.ContextMenu.Copy", "复制");
            MenuGroup.Header = Loc.Get("MainWindow.ContextMenu.Group", "分组");
            MenuDelete.Header = Loc.Get("MainWindow.ContextMenu.Delete", "删除");
            MenuBatchDelete.Header = Loc.Get("MainWindow.ContextMenu.BatchDelete", "批量删除");
            MenuBatchPaste.Header = Loc.Get("MainWindow.ContextMenu.BatchPaste", "批量粘贴");
            MenuBatchGroup.Header = Loc.Get("MainWindow.ContextMenu.BatchGroup", "批量分组");

            LoadingMoreText.Text = Loc.Get("MainWindow.Status.LoadingMore", "加载更多...");
            EmptyStateText.Text = Loc.Get("MainWindow.Status.Empty", "暂无剪贴板记录");
            LoadingText.Text = Loc.Get("MainWindow.Status.Loading", "加载中...");
            



            PanelToggleButton.ToolTip = Loc.Get("Panel.ToolTip.Toggle", "字符面板");
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
                _viewModel.OnPropertyChanged(nameof(MainViewModel.StatusText));
            });
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);

            // 注册主窗口 HWND，供 ClipboardWin32Helper 的 OpenClipboard 使用
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
                // wParam 为注册热键时分配的 id，统一交由 HotkeyService 按注册回调分发：
                // 显示/隐藏主界面、全局快捷键均通过此路径触发对应动作。
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
            
            // 将设备像素（物理像素）转换为 WPF 逻辑像素
            var mousePos = DevicePixelsToLogicalPixels(mousePosDevice, hMonitor);
            var workingArea = DevicePixelsToLogicalPixels(screen.WorkingArea, hMonitor);
            
            // 计算窗口的智能弹出位置
            var position = CalculateOptimalWindowPosition(mousePos, workingArea);
            
            Left = position.X;
            Top = position.Y;
            
            // 使用无焦点方式显示窗口
            ShowWithoutActivation();
            _isVisible = true;
            _viewModel.IsWindowVisible = true;
            
            _backgroundGcTimer?.Dispose();
            _backgroundGcTimer = null;
            
            SavePosition();

            // 优先复用隐藏时保留的缓存（文本数据、容器、选中状态）——
            // 不重新加载数据 → 列表直接显示，完全避免"清空→加载→重建"的闪烁。
            // 缓存校验：筛选未变 + 数据库总数与 GrandTotalCount 一致 → 有效 → 直接复用。
            bool cacheValid = _viewModel.IsCacheValid();
            if (!cacheValid)
            {
                _viewModel.LoadItems();
            }

            // 复用缓存时：图片缩略图已被 Trim 清空，异步触发可见区域内图片项的懒加载重绘。
            if (cacheValid)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var scroll = _listBoxScrollViewer;
                    if (scroll != null)
                    {
                        // 轻触一下滚动位置：触发 VirtualizingStackPanel 重新测量可见项，
                        // 让可见项的 Image 绑定重新读取 ImageThumbnail（触发懒加载）。
                        var offset = scroll.VerticalOffset;
                        scroll.ScrollToVerticalOffset(offset > 0 ? offset - 1 : offset + 1);
                        scroll.ScrollToVerticalOffset(offset);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            // 若没有任何项被选中则默认选中首项；避免每次显示都强制重置选中项。
            if (ClipboardListBox.SelectedItem == null && _viewModel.ClipboardItems.Count > 0)
                ClipboardListBox.SelectedIndex = 0;

            _viewModel.ReValidateAllFiles();

            if (_charPanelBuilt && CharTabControl.SelectedItem is TabItem charTab)
                LoadGroupContent(_charGroups.FirstOrDefault(g => g.Id == charTab.Tag as string) ?? _charGroups.FirstOrDefault(), CharContentPanel, "CharPanel");

            // 设置全局鼠标钩子以检测外部点击
            SetGlobalMouseHook();
        }

        private System.Drawing.Point CalculateOptimalWindowPosition(System.Drawing.Point mousePos, System.Drawing.Rectangle workingArea)
        {
            int windowWidth = (int)Width;
            int windowHeight = (int)Height;
            
            const int margin = 12; // 统一边距，参考 QuickClipboard
            
            // 默认位置：鼠标右下方
            int x = mousePos.X + margin;
            int y = mousePos.Y + margin;
            
            // 计算工作区边界
            int workRight = workingArea.Left + workingArea.Width;
            int workBottom = workingArea.Top + workingArea.Height;
            
            // 如果右边超出，移到左边
            if (x + windowWidth > workRight)
            {
                x = mousePos.X - windowWidth - margin;
            }
            
            // 如果下边超出，移到上边
            if (y + windowHeight > workBottom)
            {
                y = mousePos.Y - windowHeight - margin;
            }
            
            // 确保窗口在工作区内
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

        /// <summary>
        /// 将设备像素（物理像素）转换为 WPF 逻辑像素
        /// </summary>
        private System.Drawing.Point DevicePixelsToLogicalPixels(System.Drawing.Point devicePoint, IntPtr monitorHandle)
        {
            var dpi = GetDpiForMonitor(monitorHandle);
            return new System.Drawing.Point(
                (int)(devicePoint.X * 96.0 / dpi.X),
                (int)(devicePoint.Y * 96.0 / dpi.Y));
        }

        /// <summary>
        /// 将设备像素矩形转换为 WPF 逻辑像素矩形
        /// </summary>
        private System.Drawing.Rectangle DevicePixelsToLogicalPixels(System.Drawing.Rectangle deviceRect, IntPtr monitorHandle)
        {
            var dpi = GetDpiForMonitor(monitorHandle);
            return new System.Drawing.Rectangle(
                (int)(deviceRect.X * 96.0 / dpi.X),
                (int)(deviceRect.Y * 96.0 / dpi.Y),
                (int)(deviceRect.Width * 96.0 / dpi.X),
                (int)(deviceRect.Height * 96.0 / dpi.Y));
        }

        /// <summary>
        /// 获取指定显示器的 DPI
        /// </summary>
        private (double X, double Y) GetDpiForMonitor(IntPtr hMonitor)
        {
            uint dpiX = 96, dpiY = 96;
            try
            {
                GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, ref dpiX, ref dpiY);
            }
            catch
            {
                // 如果 API 调用失败，使用当前窗口的 DPI
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    dpiX = dpiY = (uint)GetDpiForWindow(hwnd);
                }
                else
                {
                    // 使用系统默认 DPI (96)
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

        // 焦点管理相关的系统API
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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        // 窗口样式常量
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

        // 鼠标钩子常量
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        // 鼠标钩子委托和变量
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
            _viewModel.IsWindowVisible = false;
            
            HideCustomTooltip();
            _tooltipTimer?.Stop();
            
            // 仅修剪图片缩略图等大对象，保留文本数据缓存——下次显示直接复用，避免列表刷新闪烁。
            // 符合内存清理约束（GC/Compact/WorkingSet 照常执行），数据占用内存可忽略（<几 MB）。
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

        // 焦点控制相关方法
        // 窗口样式控制方法
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

        // 焦点控制相关方法
        private void SearchTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ActivateWindow();
            SearchTextBox.Focus();
            e.Handled = false;
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

            // 左键/中键按下时捕获目标窗口句柄（供粘贴类操作使用）
            // 多选模式下点击用于选择条目，不应捕获粘贴目标
            if (!_viewModel.IsMultiSelectMode &&
                (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle))
            {
                _capturedTargetHwnd = _lastFocusHwndWhenShow != IntPtr.Zero
                    ? _lastFocusHwndWhenShow
                    : (App.GetFocusService()?.LastFocusHwnd ?? IntPtr.Zero);
            }
        }

        private void ItemBorder_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ClipboardItem item)
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    // 优先匹配主界面快捷键配置
                    if (TryMatchQuickPasteShortcut(item, Models.QuickPasteMouseButton.Middle, out var matched))
                    {
                        e.Handled = true;
                        ExecuteQuickPasteAction(item, matched.Action, matched.QuickCommandName);
                        return;
                    }
                    // 未匹配配置：维持原有「中键纯文本粘贴（去换行）」行为（向后兼容）
                    if (item.Type == ClipboardType.Text || item.Type == ClipboardType.RichText)
                    {
                        e.Handled = true;
                        PasteItemAsPlainTextRemoveNewlines(item);
                    }
                }
                else if (e.ChangedButton == MouseButton.Left)
                {
                    // 左键快捷键匹配已移至 ClipboardListBox_PreviewMouseLeftButtonUp 处理。
                    // 原因：PreviewMouseUp 与 PreviewMouseLeftButtonUp 是不同的路由事件，
                    // 此处 e.Handled 无法阻断 ListBox 级别的 PreviewMouseLeftButtonUp 默认粘贴，
                    // 会导致快捷键操作被默认粘贴覆盖。
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

        /// <summary>
        /// 预热窗口：以透明、无激活方式完成 WPF 首次布局/渲染，
        /// 避免首次快捷键弹出主界面时因 JIT 编译、样式实例化、模板构建导致的卡顿。
        /// 同时触发 WPF 自动绑定 Application.MainWindow，使托盘右键菜单可正常弹出。
        /// </summary>
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
            if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
            {
                if (_isContextMenuOpen)
                {
                    return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
                }

                // 获取鼠标点击位置
                var mouseHookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var clickPoint = new System.Drawing.Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y);
                
                // 检查点击是否在窗口外部
                if (!IsPointInWindow(clickPoint))
                {
                    // 检查点击是否在任何打开的菜单范围内
                    if (!IsPointInAnyMenu(clickPoint))
                    {
                        // 点击在窗口外部且不在菜单范围内，隐藏窗口
                        Dispatcher.Invoke(() =>
                        {
                            if (_isVisible && !_viewModel.IsPinned)
                            {
                                HideWindow();
                            }
                        });
                    }
                }
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
            
            // 添加分组菜单项
            AddGroupMenuItem(menu, Loc.Get("Common.All", "全部"), null, currentGroupId, onSelect);
            foreach (var group in _databaseService.GetAllGroups())
            {
                AddGroupMenuItem(menu, group.Name, group.Id, currentGroupId, onSelect);
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
            // 主界面快捷键：悬停项上滚轮触发对应操作，禁用列表滚动
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

        /// <summary>获取当前鼠标悬停的剪贴板项（通过可视化树查找 ItemBorder.DataContext）。</summary>
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

            // 多选模式下点击用于选择条目，不应捕获粘贴目标
            if (!_viewModel.IsMultiSelectMode)
            {
                _capturedTargetHwnd = _lastFocusHwndWhenShow != IntPtr.Zero
                    ? _lastFocusHwndWhenShow
                    : (App.GetFocusService()?.LastFocusHwnd ?? IntPtr.Zero);
            }
        }

        private void ClipboardListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsControlElement(e.OriginalSource))
                return;

            if (ClipboardListBox.SelectedItem is ClipboardItem item)
            {
                // 优先匹配主界面快捷键配置（左键）
                // 注意：PreviewMouseLeftButtonUp 与 ItemBorder 的 PreviewMouseUp 是不同的路由事件，
                // e.Handled 不会互相阻断，因此必须在此处优先检查并拦截默认粘贴
                if (TryMatchQuickPasteShortcut(item, Models.QuickPasteMouseButton.Left, out var matched))
                {
                    e.Handled = true;
                    ExecuteQuickPasteAction(item, matched.Action, matched.QuickCommandName);
                    return;
                }
                // 未匹配配置：维持原有默认左键行为
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
            string url = IsValidUrl(content) ? content : App.SettingsService.GetSearchUrl(content);

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
                MessageBox.Show($"{Loc.Get("Message.Error", "错误")}: {ex.Message}", Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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

        internal static bool IsValidUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (Uri.TryCreate(text, UriKind.Absolute, out Uri uriResult))
            {
                return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
            }

            if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate("http://" + text, UriKind.Absolute, out _);
            }

            return false;
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is not ClipboardItem item)
                return;
            if (item.Type != ClipboardType.Text && item.Type != ClipboardType.RichText)
            {
                MessageBox.Show(Loc.Get("MainWindow.Message.OnlyTextEdit", "只能编辑文本类型的内容"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var wasRichText = item.Type == ClipboardType.RichText;
            var editWindow = new EditItemWindow(item) { Owner = this };

            // 编辑窗口弹出期间按下 PinButton，确保主窗口置顶以便对照编辑
            var wasPinnedBefore = _viewModel.IsPinned;
            _viewModel.IsPinned = true;
            Topmost = true;

            try
            {
                if (editWindow.ShowDialog() == true)
                {
                    if (wasRichText && item.Type == ClipboardType.Text)
                    {
                        // 富文本经编辑后转换为纯文本：清空富文本字段并修改类型
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
                // 编辑窗口关闭后恢复 PinButton 原来的状态
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

            // 保存当前目标窗口句柄（在打开CharPickerWindow之前，避免焦点变化影响）
            var focusService = App.GetFocusService();
            var targetHwnd = focusService?.LastFocusHwnd ?? IntPtr.Zero;

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

            // 创建窗口前检查内容是否超出二维码容量上限，超限则直接提示，不弹空白窗口
            if (QRCodeWindow.IsContentTooLarge(text))
            {
                var byteCount = System.Text.Encoding.UTF8.GetByteCount(text);
                var msg = string.Format(
                    Loc.Get("QRCode.Error.TooLarge",
                        "内容过长，超出二维码容量上限（最多 {0} 字节，约 {1} 个汉字），生成失败。\n当前内容：{2} 字节（{3} 字符）"),
                    QRCodeWindow.MaxQrBytes, QRCodeWindow.MaxQrBytes / 3, byteCount, text.Length);
                MessageBox.Show(msg,
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

        private void StatusTextTip_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Panel panel)
                panel.ToolTip = Loc.Get("ClipboardType.Text", "文本");
        }

        private void StatusImageTip_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Panel panel)
                panel.ToolTip = Loc.Get("ClipboardType.Image", "图片");
        }

        private void StatusFileTip_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Panel panel)
                panel.ToolTip = Loc.Get("ClipboardType.FileList", "文件");
        }

        private void StatusRichTextTip_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Panel panel)
                panel.ToolTip = Loc.Get("ClipboardType.RichText", "富文本");
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

        /// <summary>纯文本粘贴并移除换行符。</summary>
        private void PasteRemoveNewlines_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                PasteItemAsPlainTextRemoveNewlines(item);
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

                // 动态刷新「快捷命令」二级菜单末尾的自定义快捷命令列表 + 过滤 + 隐藏空菜单
                RefreshQuickCommandSubmenu(contextMenu);
                UpdateQuickCommandsTopLevelVisibility(contextMenu);

                if (_viewModel.IsPinned)
                {
                    Topmost = false;
                }

                EnsureContextMenuTopmost(contextMenu);
            }
        }

        /// <summary>
        /// 计算「快捷命令」是否有任何可见的二级菜单项。如果没有，则将一级菜单项本身隐藏。
        /// 可见判定：Visibility == Visible 且 不是 Separator 且不是 __QC_EMPTY__ 占位。
        /// </summary>
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

        /// <summary>
        /// 刷新「快捷命令」二级菜单：
        ///   1. 找到 Separator（Tag=__QC_SEP__）位置
        ///   2. 移除该 Separator 之后的所有动态项（Tag 以 "QC:" 开头 或 Tag="__QC_EMPTY__"）
        ///   3. 追加当前 AppSettings.QuickCommands 列表里的**适用当前 item 类型**的快捷命令菜单项
        /// </summary>
        private void RefreshQuickCommandSubmenu(ContextMenu contextMenu)
        {
            if (contextMenu == null || MenuQuickCommands == null) return;
            var parent = MenuQuickCommands.Items;
            if (parent == null) return;

            // 当前选中的剪贴板 item：QC 只对 文本/富文本 生效；过滤条件：qc.ClipboardType==-1 或 qc.ClipboardType==(int)curItem.Type
            var curItem = contextMenu.PlacementTarget is System.Windows.Controls.ListBox lb ? lb.SelectedItem as ClipboardItem : null;
            int curType = curItem != null ? (int)curItem.Type : -1;
            bool canApplyToItem = curItem != null && (curItem.Type == ClipboardType.Text || curItem.Type == ClipboardType.RichText);

            // 1. 找到 Separator 的索引，之后的都是动态项
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

            // 2. 从后往前清掉 Separator 之后的所有动态项
            for (int i = parent.Count - 1; i > sepIdx; i--)
                parent.RemoveAt(i);

            var list = App.SettingsService?.Settings?.QuickCommands;
            bool anyUser = false;
            if (list != null)
            {
                // 3. 追加每个快捷命令（按当前 item 类型过滤）
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        #region 主界面快捷键

        /// <summary>
        /// 尝试匹配主界面快捷键配置。
        /// 匹配条件：鼠标按键相同 && 修饰键组合相同 && (配置类型为所有类型 || 配置类型 == 项类型)。
        /// </summary>
        private bool TryMatchQuickPasteShortcut(ClipboardItem item, Models.QuickPasteMouseButton button, out Models.QuickPasteShortcut matched)
        {
            matched = null;
            if (_quickPasteShortcuts == null || _quickPasteShortcuts.Count == 0)
                return false;

            // 隐藏/失焦后卡住导致误匹配主界面快捷键（详见 PasteItemWithKeyboardModifiers 注释）。
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

        /// <summary>根据操作类型执行对应操作（复用现有函数）。</summary>
        private void ExecuteQuickPasteAction(ClipboardItem item, Models.QuickPasteAction action, string quickCommandName = null)
        {
            // 统一以 item 为操作对象；部分事件处理类函数依赖 SelectedItem，故先选中
            ClipboardListBox.SelectedItem = item;
            switch (action)
            {
                case Models.QuickPasteAction.PasteAsPlainText:
                    PasteItemAsPlainText(item);
                    break;
                case Models.QuickPasteAction.PasteRemoveNewlines:
                    PasteItemAsPlainTextRemoveNewlines(item);
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
                    // 使用 quickCommandName 到设置里查找用户配置的快捷命令（规则链）
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
                    // 未找到配置：按 PasteRemoveNewlines（安全默认）处理，丢弃已废弃的 regexPattern 参数
                    PasteItemAsPlainTextRemoveNewlines(item);
                    break;
            }
        }

        /// <summary>
        /// 全局快捷键动作执行：根据 <see cref="GlobalHotkey"/> 配置，对当前列表中第 N 项剪贴板条目
        /// （ItemIndex 为 1 基索引）执行指定操作。窗口处于隐藏状态时也能工作——粘贴类操作通过
        /// FocusService 解析用户当前前台窗口作为粘贴目标。
        /// </summary>
        public void ExecuteGlobalHotkeyAction(GlobalHotkey gh)
        {
            if (gh == null) return;

            int idx = gh.ItemIndex - 1;
            if (idx < 0) idx = 0;

            // 确保列表已加载：窗口隐藏期间集合仍维护最近 200 条，可直接取用。
            var items = _viewModel?.ClipboardItems;
            if (items == null || items.Count == 0)
            {
                _viewModel?.LoadItems();
                items = _viewModel?.ClipboardItems;
            }
            if (items == null || idx >= items.Count) return;

            var item = items[idx];
            if (item == null) return;

            // 复用既有动作分发逻辑；粘贴类动作内部会通过 FocusService 定位目标窗口。
            ExecuteQuickPasteAction(item, gh.Action, gh.QuickCommandName);
        }

        /// <summary>
        /// 快捷命令规则链执行：按顺序依次对文本应用规则列表中 Enabled=true 的条目，
        /// 每条规则执行时单独捕获异常并弹窗提示，最后把结果设置回剪贴板并粘贴。
        /// </summary>
        /// <param name="item">剪贴板条目（必须是 Text 或 RichText）。</param>
        /// <param name="rules">规则列表。</param>
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
                        current = ApplyQuickCommandRule(current, rule.Function, p1, p2, firstOnly);
                    }
                    catch (Exception ruleEx)
                    {
                        string err = string.Format(
                            Loc.Get("MainWindow.Message.QcRuleFailed", "快捷命令执行失败：规则 #{0} [{1}] 报错：{2}"),
                            stepIndex, fnName, ruleEx.Message);
                        MessageBox.Show(err, Loc.Get("Message.Error", "错误"),
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                ClipboardWin32Helper.SetText(current ?? "");
                PostPasteFlow(pasteHashText: current ?? "", moveTopItem: item);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message),
                    Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        /// <summary>对一段文本执行单个快捷命令函数。</summary>
        private static string ApplyQuickCommandRule(string input,
            Models.QuickCommandFunction func, string param1, string param2, bool firstOnly)
        {
            if (input == null) input = "";
            switch (func)
            {
                case Models.QuickCommandFunction.StringReplace:
                    if (string.IsNullOrEmpty(param1)) return input;
                    if (firstOnly)
                    {
                        int idx = input.IndexOf(param1, StringComparison.Ordinal);
                        if (idx < 0) return input;
                        return input.Substring(0, idx) + (param2 ?? "") +
                               input.Substring(idx + param1.Length);
                    }
                    return input.Replace(param1, param2 ?? "");

                case Models.QuickCommandFunction.RegexReplace:
                    if (string.IsNullOrEmpty(param1)) return input;
                    var rr = new System.Text.RegularExpressions.Regex(param1);
                    if (firstOnly) return rr.Replace(input, param2 ?? "", 1);
                    return rr.Replace(input, param2 ?? "");

                case Models.QuickCommandFunction.RegexMatches:
                    // 语义：从文本中提取所有 Pattern 匹配的片段。
                    //   Param1 = Pattern
                    //   Param2 = 分隔符（空或 null => 直接拼接；非空 => 用指定分隔符连接多个 Match.Value）
                    // Pattern 为空 或 匹配不到 => 返回空串（绝不返回原文，避免误导性"去空格后原文还在"。
                    if (string.IsNullOrEmpty(param1)) return "";
                    var rm2 = new System.Text.RegularExpressions.Regex(param1);
                    var mc2 = rm2.Matches(input);
                    if (mc2.Count == 0) return "";
                    if (string.IsNullOrEmpty(param2))
                    {
                        var sb2 = new System.Text.StringBuilder();
                        foreach (System.Text.RegularExpressions.Match m in mc2)
                            sb2.Append(m.Value);
                        return sb2.ToString();
                    }
                    // Param2 作为分隔符连接
                    var list = new System.Collections.Generic.List<string>(mc2.Count);
                    foreach (System.Text.RegularExpressions.Match m2 in mc2)
                        list.Add(m2.Value);
                    return string.Join(param2, list);

                case Models.QuickCommandFunction.RegexEscape:
                    return System.Text.RegularExpressions.Regex.Escape(input);

                case Models.QuickCommandFunction.StringTrim:
                    return input.Trim();

                case Models.QuickCommandFunction.StringToUpper:
                    return input.ToUpperInvariant();

                case Models.QuickCommandFunction.StringToLower:
                    return input.ToLowerInvariant();

                case Models.QuickCommandFunction.RegexSplit:
                    if (string.IsNullOrEmpty(param1)) return input;
                    var parts = System.Text.RegularExpressions.Regex.Split(input, param1);
                    return string.Join(Environment.NewLine, parts);

                case Models.QuickCommandFunction.DistinctRemoveDuplicate:
                    // 按行去重：统一换行 → 拆分 → 保留首次出现顺序 → 用原换行风格拼回
                    string newLine = "\n";
                    if (input.Contains("\r\n")) newLine = "\r\n";
                    else if (input.Contains("\r")) newLine = "\r";
                    string[] lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    var seen = new System.Collections.Generic.HashSet<string>();
                    var unique = new System.Collections.Generic.List<string>();
                    foreach (var line in lines)
                    {
                        if (seen.Add(line)) unique.Add(line);
                    }
                    return string.Join(newLine, unique);

                case Models.QuickCommandFunction.RemoveWhiteSpace:
                    var nosp = new System.Text.StringBuilder(input.Length);
                    foreach (char c in input)
                    {
                        if (!char.IsWhiteSpace(c)) nosp.Append(c);
                    }
                    return nosp.ToString();

                case Models.QuickCommandFunction.RemoveNewLines:
                    return input.Replace("\r", "").Replace("\n", "");

                default:
                    return input;
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (targetHwnd == IntPtr.Zero) return;

            BringWindowToForeground(targetHwnd);

            // 自适应等待：检测到窗口激活立即继续，超时才等待
            WaitForWindowActivated(targetHwnd);

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

            // 未检测到激活，保底等待让目标窗口完成输入队列准备
            Thread.Sleep(30);
        }

        private static void BringWindowToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr rootHwnd = GetAncestor(hWnd, GA_ROOT);
            if (rootHwnd != IntPtr.Zero)
            {
                hWnd = rootHwnd;
            }

            // 先放宽 Windows 前台窗口锁定超时限制，
            // 这是 CopyQ 等成熟客户端的通用做法，大幅提升 SetForegroundWindow 成功率。
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);

            uint foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            uint currentThreadId = GetCurrentThreadId();

            bool attached = false;
            if (foregroundThreadId != currentThreadId && foregroundThreadId != 0)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
                attached = true;
            }

            try
            {
                SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }

            // 验证窗口是否已成为前台
            IntPtr newForeground = GetForegroundWindow();
            IntPtr newRoot = GetAncestor(newForeground, GA_ROOT);
            if (newRoot != hWnd && newForeground != hWnd)
            {
                // SetForegroundWindow 可能因 Windows 前台窗口限制而失败，
                // 使用模拟 Alt 键按下/释放的方式触发系统允许前台切换的机制。
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                SetForegroundWindow(hWnd);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
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

            // 解析目标窗口句柄：优先使用调用方显式指定的，其次使用 PreviewMouseDown 时捕获的，
            // 再使用 ShowAtCursor 时的快照，接着 FocusService 实时记录，最后回退到窗口栈。
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
            else if (_lastFocusHwndWhenShow != IntPtr.Zero)
            {
                targetHwnd = _lastFocusHwndWhenShow;
            }
            else if (focusService != null)
            {
                targetHwnd = focusService.LastFocusHwnd;
                if (targetHwnd == IntPtr.Zero)
                {
                    targetHwnd = focusService.GetPreviousFocusHwnd(1);
                }
                if (targetHwnd == IntPtr.Zero)
                {
                    targetHwnd = focusService.GetPreviousFocusHwnd(2);
                }
            }

            // ★ 关键修复：先隐藏 WinVClip 窗口，让操作系统恢复目标窗口的前台焦点。
            // 否则 WinVClip 占据前台时 SetForegroundWindow 会被 Windows 拒绝。
            // 全局快捷键触发时窗口通常已隐藏，此时无需重复执行 HideWindow（避免冗余的清理与 GC）。
            var windowStateService = App.GetWindowStateService();
            bool shouldHide = _isVisible && windowStateService != null && !windowStateService.IsPinned;

            if (shouldHide)
            {
                HideWindow();
            }

            // 使用 BeginInvoke 延迟执行粘贴逻辑，确保 WinVClip 已完全隐藏、
            // 目标窗口已重新获得前台焦点后再进行窗口激活和按键注入。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActivateAndPaste(targetHwnd);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static IntPtr GetDesktopWindow()
        {
            // 尝试找到桌面窗口（Progman 或 WorkerW）
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.CopyFailed", "复制失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            // 清除可能残留的捕获目标，避免多选模式下使用过期句柄
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.PasteFailed", "粘贴失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (MessageBox.Show(string.Format(Loc.Get("MainWindow.Message.ConfirmDeleteSelected", "确定要删除选中的 {0} 项吗？"), selectedItems.Count), 
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

        private List<ClipboardItem> GetSelectedItemsOrShowMessage(string emptyMessage)
        {
            var selectedItems = _viewModel.GetSelectedItems();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(emptyMessage, Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            return selectedItems;
        }

        private void ItemGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is Grid grid)
            {
                var diff = _dragStartPoint - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (grid.DataContext is ClipboardItem item)
                    {
                        StartDrag(item);
                    }
                }
            }
            else
            {
                _dragStartPoint = e.GetPosition(null);
            }
        }

        private void StartDrag(ClipboardItem item)
        {
            var data = new DataObject();
            SetClipboardContent(item, data);
            DragDrop.DoDragDrop(ClipboardListBox, data, DragDropEffects.Copy);
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
                // 先隐藏主窗口，再延时 100ms 弹出系统字符面板，
                // 避免 WinVClip 窗口抢占焦点导致系统字符面板闪退或无法正常输入。
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

                var data = System.Text.Json.JsonSerializer.Deserialize<CharDataRoot>(json);
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
