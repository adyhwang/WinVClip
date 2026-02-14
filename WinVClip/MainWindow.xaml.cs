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
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
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
                    ClipboardType.Text => "文本",
                    ClipboardType.Image => "图片",
                    ClipboardType.FileList => "文件",
                    _ => "未知"
                };
            }
            return "未知";
        }
    }

    public class ClipboardTypeToBackgroundConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardType type)
            {
                return type switch
                {
                    ClipboardType.Text => new SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                    ClipboardType.Image => new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
                    ClipboardType.FileList => new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))
                };
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
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

    public class ItemTypeToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ClipboardItem item && item.Type == ClipboardType.Text
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public class ItemTypeToUrlVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ClipboardItem item || item.Type != ClipboardType.Text)
                return Visibility.Collapsed;

            string content = item.Content?.Trim() ?? "";
            return IsUrl(content) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (Uri.TryCreate(text, UriKind.Absolute, out Uri uriResult))
                return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;

            if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                return Uri.TryCreate("http://" + text, UriKind.Absolute, out _);

            return false;
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

            if (item.Type == ClipboardType.Text)
            {
                string content = item.Content?.Trim() ?? "";
                if (IsLocalPath(content))
                    return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private static bool IsLocalPath(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                if (File.Exists(text) || Directory.Exists(text))
                    return true;
            }
            catch
            {
            }

            return false;
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

    public class ByteArrayToImageConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not byte[] imageData || imageData.Length == 0)
                    return null;

                using var stream = new MemoryStream(imageData);
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }

    public class StringToVisibilityConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public class TypeFilterToBackgroundConverter : BaseConverter
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
                    _ => false
                };
                
                return isActive ? "#007ACC" : "{DynamicResource IconButtonBackground}";
            }
            return "{DynamicResource IconButtonBackground}";
        }
    }

    public class GroupFilterToBackgroundConverter : BaseConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (int.TryParse(parameter?.ToString(), out int targetValue))
            {
                long? currentFilter = value as long?;
                bool isActive = targetValue switch
                {
                    0 => !currentFilter.HasValue, // 全部
                    1 => currentFilter.HasValue,  // 当前分组
                    _ => false
                };
                
                return isActive ? "#007ACC" : "{DynamicResource IconButtonBackground}";
            }
            return "{DynamicResource IconButtonBackground}";
        }
    }

    public class IsSelectedConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is ClipboardItem item && values[1] is MainViewModel viewModel)
            {
                return viewModel.IsMultiSelectMode && viewModel.SelectedItemIds.Contains(item.Id);
            }
            return false;
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

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var previewLines = lines.Take(10).ToList();
            var result = string.Join("\n", previewLines);
            var suffix = lines.Length > 10 ? "……" : "";
            
            return $"{result}\n{suffix}(共{content.Length}个字符)";
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
            var folderCount = filePaths.Count(p => Directory.Exists(p));
            var fileCount = filePaths.Count - folderCount;
            
            return folderCount > 0 && fileCount > 0
                ? $"……(共{fileCount}个文件、{folderCount}个目录)"
                : folderCount > 0
                    ? $"……(共{folderCount}个目录)"
                    : $"……(共{fileCount}个文件)";
        }
    }

    public class ImageCache
    {
        private static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();
        private static readonly object _lock = new object();

        public static BitmapImage GetImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);
            
            lock (_lock)
            {
                if (_cache.TryGetValue(fullPath, out var cachedImage))
                    return cachedImage;

                try
                {
                    if (File.Exists(fullPath))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(fullPath);
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 500; // 限制解码尺寸，提高性能
                        image.EndInit();
                        image.Freeze(); // 允许跨线程访问

                        _cache[fullPath] = image;
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
        private readonly System.Timers.Timer _searchDelayTimer;

        public ObservableCollection<ClipboardItem> ClipboardItems { get; } = new ObservableCollection<ClipboardItem>();

        public string Hotkey => _settingsService.Settings.Hotkey;

        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                _isPinned = value;
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
                    SelectedItemIds.Clear();
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
                OnPropertyChanged(nameof(IsFormatFilterActive));
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
                OnPropertyChanged(nameof(IsGroupFilterActive));
                OnPropertyChanged(nameof(GroupFilterName));
                LoadItems();
            }
        }

        public bool IsFormatFilterActive => TypeFilter.HasValue;

        public bool IsGroupFilterActive => GroupFilter.HasValue;

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

        private Group _selectedGroup;
        public Group SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value;
                OnPropertyChanged();
                if (value != null && value.Id > 0)
                {
                    GroupFilter = value.Id;
                }
                else
                {
                    GroupFilter = null;
                }
            }
        }

        public string CurrentGroupName => GetGroupName(GroupFilter, "全部");

        public void LoadGroups()
        {
            Groups.Clear();
            Groups.Add(new Group { Id = 0, Name = "全部" });
            var groups = _databaseService.GetAllGroups();
            foreach (var group in groups)
            {
                Groups.Add(group);
            }
            Groups.Add(new Group { Id = -1L, Name = "管理分组" });
        }

        public bool HasItems => ClipboardItems.Count > 0;

        public bool IsEmpty => ClipboardItems.Count == 0;

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

        public string GroupFilterName => GetGroupName(GroupFilter, string.Empty);

        private string GetGroupName(long? groupId, string defaultValue)
        {
            if (!groupId.HasValue)
                return defaultValue;
            
            var group = _databaseService.GetAllGroups().FirstOrDefault(g => g.Id == groupId.Value);
            return group?.Name ?? defaultValue;
        }

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
            _searchDelayTimer = new System.Timers.Timer(300);
            _searchDelayTimer.AutoReset = false;
            _searchDelayTimer.Elapsed += (s, e) => Application.Current.Dispatcher.Invoke(LoadItems);
            LoadItems();
        }

        public async Task LoadItemsAsync()
        {
            IsLoading = true;
            try
            {
                List<ClipboardItem> newItems = null;
                await Task.Run(() =>
                {
                    newItems = _databaseService.GetItems(100, 0, SearchText,
                        TypeFilter, GroupFilter);
                });
                
                if (newItems == null)
                    return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateItemsDiff(newItems);
                    OnPropertyChanged(nameof(HasItems));
                    OnPropertyChanged(nameof(IsEmpty));
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateItemsDiff(List<ClipboardItem> newItems)
        {
            var newItemIds = new HashSet<long>(newItems.Select(i => i.Id));
            var currentItemIds = new HashSet<long>(ClipboardItems.Select(i => i.Id));

            var itemsToRemove = ClipboardItems.Where(i => !newItemIds.Contains(i.Id)).ToList();
            foreach (var item in itemsToRemove)
            {
                ClipboardItems.Remove(item);
            }

            for (int i = 0; i < newItems.Count; i++)
            {
                var newItem = newItems[i];
                if (i < ClipboardItems.Count)
                {
                    if (ClipboardItems[i].Id != newItem.Id)
                    {
                        var existingIndex = ClipboardItems.ToList().FindIndex(ci => ci.Id == newItem.Id);
                        if (existingIndex >= 0)
                        {
                            ClipboardItems.Move(existingIndex, i);
                        }
                        else
                        {
                            ClipboardItems.Insert(i, newItem);
                        }
                    }
                }
                else
                {
                    ClipboardItems.Add(newItem);
                }
            }

            while (ClipboardItems.Count > newItems.Count)
            {
                ClipboardItems.RemoveAt(ClipboardItems.Count - 1);
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
            ClipboardItems.Insert(0, item);
            if (ClipboardItems.Count > 200)
            {
                ClipboardItems.RemoveAt(ClipboardItems.Count - 1);
            }
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
            ItemAdded?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveItem(ClipboardItem item)
        {
            ClipboardItems.Remove(item);
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public partial class MainWindow : Window
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
        private bool _isPasting = false; // 防止快速点击时的并发执行

        public MainViewModel ViewModel => _viewModel;

        public MainWindow(DatabaseService databaseService, SettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            _viewModel = new MainViewModel(databaseService, settingsService);

            DataContext = _viewModel;
            InitializeComponent();

            _savedLeft = Left;
            _savedTop = Top;
            
            SourceInitialized += MainWindow_SourceInitialized;
            _settingsService.SettingsChanged += OnSettingsChanged;
            _viewModel.ItemAdded += OnItemAdded;
        }

        private void OnItemAdded(object sender, EventArgs e)
        {
            // 新记录添加时，滚动到顶部
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
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                App.ToggleMainWindow();
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
            
            SavePosition();
            
            _viewModel.LoadItems();
            ClipboardListBox.SelectedIndex = 0;
            
            // 设置全局鼠标钩子以检测外部点击
            SetGlobalMouseHook();
        }

        private System.Drawing.Point CalculateOptimalWindowPosition(System.Drawing.Point mousePos, System.Drawing.Rectangle workingArea)
        {
            int windowWidth = (int)Width;
            int windowHeight = (int)Height;
            
            // 优先尝试在鼠标右下角显示
            var x = mousePos.X + 10;
            var y = mousePos.Y + 10;
            
            // 检查右下角是否有足够空间
            bool canFitRight = x + windowWidth <= workingArea.Right;
            bool canFitBottom = y + windowHeight <= workingArea.Bottom;
            
            if (canFitRight && canFitBottom)
            {
                // 右下角有足够空间，直接使用
                return new System.Drawing.Point(x, y);
            }
            
            // 右下角空间不足，尝试其他位置
            if (canFitRight && !canFitBottom)
            {
                // 右边有空间，但下面没有，尝试右上角
                y = mousePos.Y - windowHeight - 10;
                if (y >= workingArea.Top)
                {
                    return new System.Drawing.Point(x, y);
                }
                
                // 右上角也没有空间，调整到屏幕底部
                y = workingArea.Bottom - windowHeight;
                return new System.Drawing.Point(x, y);
            }
            
            if (!canFitRight && canFitBottom)
            {
                // 下面有空间，但右边没有，尝试左下角
                x = mousePos.X - windowWidth - 10;
                if (x >= workingArea.Left)
                {
                    return new System.Drawing.Point(x, y);
                }
                
                // 左下角也没有空间，调整到屏幕右边
                x = workingArea.Right - windowWidth;
                return new System.Drawing.Point(x, y);
            }
            
            // 右下角、右上角、左下角都没有足够空间，尝试左上角
            x = mousePos.X - windowWidth - 10;
            y = mousePos.Y - windowHeight - 10;
            
            if (x >= workingArea.Left && y >= workingArea.Top)
            {
                return new System.Drawing.Point(x, y);
            }
            
            // 所有角落都没有足够空间，调整到屏幕可见区域
            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - windowWidth));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - windowHeight));
            
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
        private static extern IntPtr GetForegroundWindow();

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
        private static extern bool ScreenToClient(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        private static extern bool PtInRect(ref RECT lprc, System.Drawing.Point pt);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // 窗口样式常量
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;

        // 鼠标钩子常量
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        // 鼠标钩子委托和变量
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private bool _isMouseHookSet = false;

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

        private void RestorePosition()
        {
            Left = _savedLeft;
            Top = _savedTop;
        }

        private void HideAndSave()
        {
            SavePosition();
            Hide();
            _isVisible = false;
            
            // 移除全局鼠标钩子
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

        private void ComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SetWindowActivateStyle(true);
            e.Handled = false;
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            SetWindowActivateStyle(true);
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            DeactivateWindowDeferred();
        }

        private void ItemBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SetWindowActivateStyle(true);
            e.Handled = false;
            DeactivateWindowDeferred();
        }

        private void ShowWithoutActivation()
        {
            SetWindowActivateStyle(false);
            Show();
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, (IntPtr)(-1), 0, 0, 0, 0, 
                SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
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
                // 获取鼠标点击位置
                var mouseHookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var clickPoint = new System.Drawing.Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y);
                
                // 检查点击是否在窗口外部
                if (!IsPointInWindow(clickPoint))
                {
                    // 点击在窗口外部，隐藏窗口
                    Dispatcher.Invoke(() =>
                    {
                        if (_isVisible && !_viewModel.IsPinned)
                        {
                            HideWindow();
                        }
                    });
                }
            }
            
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
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

        private void ShowAndRestore()
        {
            RestorePosition();
            Show();
            Activate();
            Focus();
            _isVisible = true;
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
            _viewModel.IsFilterPanelVisible = !_viewModel.IsFilterPanelVisible;
            if (_viewModel.IsFilterPanelVisible)
            {
                _viewModel.LoadGroups();
            }
            else
            {
                _viewModel.TypeFilter = null;
                _viewModel.GroupFilter = null;
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

        private void FilterImage_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = (int)ClipboardType.Image;
        }

        private void FilterFile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TypeFilter = (int)ClipboardType.FileList;
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupComboBox.SelectedItem is Group group)
            {
                if (group.Id == -1L)
                {
                    // 打开分组管理窗口
                    var groupWindow = new GroupManageWindow(_databaseService);
                    groupWindow.Owner = this;
                    groupWindow.ShowDialog();
                    // 重新加载分组
                    _viewModel.LoadGroups();
                    // 重置选择为"全部"
                    GroupComboBox.SelectedIndex = 0;
                }
                else
                {
                    _viewModel.SelectedGroup = group;
                }
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
            
            // 添加分组菜单项
            AddGroupMenuItem(menu, "全部", null, currentGroupId, onSelect);
            
            foreach (var group in _databaseService.GetAllGroups())
            {
                AddGroupMenuItem(menu, group.Name, group.Id, currentGroupId, onSelect);
            }
            
            menu.Items.Add(new Separator { Style = TryFindResource("SeparatorStyle") as Style });
            
            // 管理选项
            var manageItem = new MenuItem { Header = "⚙️ 管理", Style = TryFindResource("MenuItemStyle") as Style };
            manageItem.Click += (s, args) => 
            {
                var manageWindow = new GroupManageWindow(_databaseService) { Owner = this };
                if (manageWindow.ShowDialog() == true)
                {
                    _viewModel.LoadItems();
                }
            };
            menu.Items.Add(manageItem);
            
            menu.PlacementTarget = placementTarget;
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



        private void ClipboardListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainScrollViewer != null)
            {
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void ClipboardListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsControlElement(e.OriginalSource))
                return;

            if (ClipboardListBox.SelectedItem is ClipboardItem item)
            {
                if (_viewModel.IsMultiSelectMode)
                    _viewModel.ToggleSelection(item);
                else
                    PasteItem(item);
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
                case Key.Enter:
                    if (ClipboardListBox.SelectedItem is ClipboardItem item)
                    {
                        PasteItem(item);
                    }
                    break;
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
            if (item.Type != ClipboardType.Text)
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
            else if (item.Type == ClipboardType.Text)
            {
                string content = item.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (File.Exists(content))
                        folderPath = Path.GetDirectoryName(content);
                    else if (Directory.Exists(content))
                        folderPath = content;
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
                MessageBox.Show($"{errorMessage}: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsValidUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            // 尝试直接解析为Uri
            if (Uri.TryCreate(text, UriKind.Absolute, out Uri uriResult))
            {
                return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
            }

            // 处理以www.开头的情况
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
            if (item.Type != ClipboardType.Text)
            {
                MessageBox.Show("只能编辑文本类型的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editWindow = new EditItemWindow(item) { Owner = this };
            if (editWindow.ShowDialog() == true)
            {
                _databaseService.UpdateItemContent(item.Id, item.Content);
                _viewModel.LoadItems();
            }
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                CopyToClipboard(item);
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

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ClipboardListBox.SelectedItem is ClipboardItem item)
                DeleteItem(item);
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            // 打开上下文菜单
            ClearHistoryContextMenu.PlacementTarget = ClearHistoryButton;
            ClearHistoryContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            ClearHistoryContextMenu.IsOpen = true;
        }

        private void ClearAllHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要清空所有剪贴板历史记录吗？\n\n此操作将删除所有记录（包括已分组的记录）。",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _databaseService.ClearHistory();
                _viewModel.LoadItems();
            }
        }

        private void ClearUngroupedHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要清空所有未分组的剪贴板历史记录吗？\n\n已分组的记录将保留。", 
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _databaseService.ClearUngroupedHistory();
                _viewModel.LoadItems();
            }
        }

        private void SetClipboardContent(ClipboardItem item, DataObject dataObject = null)
        {
            switch (item.Type)
            {
                case ClipboardType.Text:
                    SetClipboardData(dataObject, d => d.SetText(item.Content), () => Clipboard.SetText(item.Content));
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

            SetClipboardData(dataObject, d => d.SetImage(image), () => Clipboard.SetImage(image));
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
                Clipboard.SetDataObject(fileDataObject);
            }
        }

        private void PasteItem(ClipboardItem item)
        {
            if (_isPasting)
                return;

            try
            {
                _isPasting = true;

                SetClipboardContent(item);
                App.GetClipboardMonitor()?.IgnoreNextChange(1000);

                if (App.SettingsService.Settings.MoveToTopAfterPaste && !item.GroupId.HasValue)
                {
                    _databaseService.UpdateItemTimestampById(item.Id);
                    _viewModel.LoadItems();
                }

                var activeWindowHandle = GetForegroundWindow();

                if (!_viewModel.IsPinned)
                {
                    HideWindow();
                    Thread.Sleep(50);
                }
                else
                {
                    Thread.Sleep(30);
                }

                ActivateAndPaste(activeWindowHandle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private void ActivateAndPaste(IntPtr windowHandle)
        {
            if (windowHandle != IntPtr.Zero)
            {
                SetForegroundWindow(windowHandle);
                Thread.Sleep(20);
            }
            SimulatePaste();
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void SimulatePaste()
        {
            keybd_event(VK_CONTROL, 0, 0, 0);
            keybd_event(VK_V, 0, 0, 0);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
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
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteItem(ClipboardItem item)
        {
            _databaseService.DeleteItem(item.Id);
            _viewModel.RemoveItem(item);
        }

        private void EnterMultiSelectMode_Click(object sender, RoutedEventArgs e) 
            => SetMultiSelectMode(true);

        private void ExitMultiSelectMode_Click(object sender, RoutedEventArgs e) 
            => SetMultiSelectMode(false);

        private void SetMultiSelectMode(bool enabled)
        {
            _viewModel.IsMultiSelectMode = enabled;
            _viewModel.ClearSelection();
        }

        private void BatchDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItemsOrShowMessage("请先选择要删除的项");
            if (selectedItems == null) return;

            if (MessageBox.Show($"确定要删除选中的 {selectedItems.Count} 项吗？", 
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                    _databaseService.DeleteItem(item.Id);
                
                _viewModel.ClearSelection();
                _viewModel.LoadItems();
            }
        }

        private void BatchGroup_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItemsOrShowMessage("请先选择要分组的项");
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
                MessageBox.Show(emptyMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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

                _tooltipTimer?.Stop();
                _tooltipTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(item.PreviewDelay)
                };
                _tooltipTimer.Tick += (s, args) =>
                {
                    _tooltipTimer.Stop();
                    if (_currentTooltipItem == item && border.IsMouseOver)
                    {
                        ShowCustomTooltip(border, item);
                    }
                };
                _tooltipTimer.Start();
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
    }
}
