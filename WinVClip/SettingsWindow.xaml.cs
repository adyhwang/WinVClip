using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _originalSettings;
        private bool _isSettingHotkey = false;
        private string _tempHotkey = "";
        private readonly HotkeyService _hotkeyService;
        private readonly List<string> _pressedModifiers = new List<string>();
        private bool _originalStartWithWindows;

        public SettingsWindow(SettingsService settingsService)
        {
            _settingsService = settingsService;
            // 从计划任务读取开机启动状态
            _originalStartWithWindows = _settingsService.IsStartupEnabled();
            
            _originalSettings = new AppSettings
            {
                Hotkey = _settingsService.Settings.Hotkey,
                CaptureImages = _settingsService.Settings.CaptureImages,
                CaptureFiles = _settingsService.Settings.CaptureFiles,
                RemoveDuplicates = _settingsService.Settings.RemoveDuplicates,
                DatabasePath = _settingsService.Settings.DatabasePath,
                EnableAutoCleanup = _settingsService.Settings.EnableAutoCleanup,
                RetentionDays = _settingsService.Settings.RetentionDays,
                MonitorEnabled = _settingsService.Settings.MonitorEnabled,
                Theme = _settingsService.Settings.Theme,
                SelectedSearchEngineId = _settingsService.Settings.SelectedSearchEngineId,
                CustomSearchEngineUrl = _settingsService.Settings.CustomSearchEngineUrl,
                MaxHistoryItems = _settingsService.Settings.MaxHistoryItems,
                IsAdministratorRun = _settingsService.Settings.IsAdministratorRun,
                MoveToTopAfterPaste = _settingsService.Settings.MoveToTopAfterPaste,
                PasteShortcutMode = _settingsService.Settings.PasteShortcutMode,
                Language = _settingsService.Settings.Language,
                FontSize = _settingsService.Settings.FontSize,
                ShowHotkeyTipOnStartup = _settingsService.Settings.ShowHotkeyTipOnStartup,
                UseSystemCharPanel = _settingsService.Settings.UseSystemCharPanel,
                QuickPasteShortcuts = _settingsService.Settings.QuickPasteShortcuts?
                    .Select(s => s.Clone()).ToList() ?? new List<QuickPasteShortcut>(),
                QuickCommands = _settingsService.Settings.QuickCommands?
                    .Select(s => s.Clone()).ToList() ?? new List<QuickCommand>(),
                GlobalHotkeys = _settingsService.Settings.GlobalHotkeys?
                    .Select(g => g.Clone()).ToList() ?? new List<GlobalHotkey>()
            };

            _tempHotkey = _originalSettings.Hotkey;
            
            var mainWindow = App.GetMainWindow();
            if (mainWindow != null)
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                _hotkeyService = new HotkeyService(helper.Handle);
            }
            else
            {
                _hotkeyService = new HotkeyService(IntPtr.Zero);
            }

            DataContext = _settingsService.Settings;
            InitializeComponent();
            
            ApplyLocalization();
            
            InitializeLanguageComboBox();
            UpdateAdminButtonState();

            if (StartWithWindowsCheckBox != null)
            {
                StartWithWindowsCheckBox.IsChecked = _originalStartWithWindows;
            }

            _settingsService.InitializeSearchEngines();
            
            if (CustomSearchEngineGrid != null)
            {
                UpdateCustomSearchEngineGridVisibility();
            }

            LoadAboutInfo();
            LoadQuickPasteEntries();
            LoadQuickCommandEntries();
            LoadGlobalHotkeyEntries();
            ValidateAllEntries();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.Invoke(() => ApplyLocalization());
        }

        private void ApplyLocalization()
        {
            Title = Loc.Get("Settings.Title", "设置");
            TitleText.Text = Loc.Get("Settings.Title", "设置");
            NavGeneral.Content = Loc.Get("Settings.Nav.General", "常规");
            NavHotkey.Content = Loc.Get("Settings.Nav.Hotkey", "快捷键");
            NavQuickCommand.Content = Loc.Get("Settings.Nav.QuickCommand", "快捷命令");
            NavCapture.Content = Loc.Get("Settings.Nav.Capture", "捕获设置");
            NavHistory.Content = Loc.Get("Settings.Nav.History", "历史记录");
            NavStorage.Content = Loc.Get("Settings.Nav.Storage", "存储与备份");
            NavSearch.Content = Loc.Get("Settings.Nav.Search", "搜索引擎");
            NavSystem.Content = Loc.Get("Settings.Nav.System", "系统设置");
            NavAbout.Content = Loc.Get("Settings.Nav.About", "关于");
            
            ResetButton.Content = Loc.Get("Settings.Button.Reset", "重置");
            ApplyButton.Content = Loc.Get("Settings.Button.Apply", "应用");
            OKButton.Content = Loc.Get("Settings.Button.OK", "确定");
            CancelButton.ToolTip = Loc.Get("Settings.Button.Cancel", "取消");
            
            ApplyPanelGeneralLocalization();
            ApplyPanelHotkeyLocalization();
            ApplyPanelQuickCommandLocalization();
            ApplyPanelCaptureLocalization();
            ApplyPanelHistoryLocalization();
            ApplyPanelStorageLocalization();
            ApplyPanelSearchLocalization();
            ApplyPanelSystemLocalization();
        }

        private void ApplyPanelGeneralLocalization()
        {
            GeneralTitleText.Text = Loc.Get("Settings.General.Title", "常规");
            HotkeyCardTitle.Text = Loc.Get("Settings.Hotkey.GlobalTitle", "全局快捷键");
            HotkeyShowHideText.Text = Loc.Get("Settings.General.Hotkey.ShowHide", "显示/隐藏主界面");
            HotkeyPlaceholderText.Text = Loc.Get("Settings.General.Hotkey.ClickToInput", "点击输入快捷键");
            CommonShortcutsText.Text = Loc.Get("Settings.General.Hotkey.CommonShortcuts", "常用快捷键");
            RestoreSystemWinVButton.Content = Loc.Get("Settings.General.Hotkey.RestoreSystemWinV", "还原系统 Win+V");
            
            AppearanceCardTitle.Text = Loc.Get("Settings.General.Appearance.Title", "外观");
            ThemeModeText.Text = Loc.Get("Settings.General.Appearance.ThemeMode", "主题模式");
            ThemeAutoItem.Content = Loc.Get("Settings.General.Appearance.ThemeAuto", "跟随系统");
            ThemeLightItem.Content = Loc.Get("Settings.General.Appearance.ThemeLight", "浅色模式");
            ThemeDarkItem.Content = Loc.Get("Settings.General.Appearance.ThemeDark", "深色模式");
            
            LanguageCardTitle.Text = Loc.Get("Settings.General.Language.Title", "Language");
            SelectLanguageText.Text = Loc.Get("Settings.General.Language.SelectLanguage", "Select Language");
            
            PasteMethodCardTitle.Text = Loc.Get("Settings.General.PasteMethod.Title", "粘贴方式");
            PasteMethodDescription.Text = Loc.Get("Settings.General.PasteMethod.Description", "选择粘贴内容的快捷键方式");
            PasteModeAutoItem.Content = Loc.Get("Settings.General.PasteMethod.Auto", "自动选择");
            PasteModeAutoHint.Text = Loc.Get("Settings.General.PasteMethod.AutoHint", "自动选择：在终端中或按住 Ctrl/Alt 时自动使用 Shift+Insert");
            
            AdminCardTitle.Text = Loc.Get("Settings.General.Admin.Title", "管理员权限");
            AutoAdminCheckBox.Content = Loc.Get("Settings.General.Admin.AutoRequest", "启动时自动获取管理员权限");
            AutoAdminDescription.Text = Loc.Get("Settings.General.Admin.AutoRequestDescription", "每次启动时自动请求管理员权限运行");
            RequestAdminButton.Content = Loc.Get("Settings.General.Admin.Request", "获取管理员权限");
            AdminRunDescription.Text = Loc.Get("Settings.General.Admin.RunDescription", "以管理员权限运行可向所有窗口发送粘贴操作");
            AdminRunHint.Text = Loc.Get("Settings.General.Admin.Hint", "提示：普通权限无法向管理员权限的窗口（如管理员PowerShell、CMD等等）自动粘贴");

            CharPanelCardTitle.Text = Loc.Get("Settings.General.CharPanel.Title", "🔣字符面板按钮");
            UseSystemCharPanelCheckBox.Content = Loc.Get("Settings.General.CharPanel.UseSystem", "是否调用系统的字符面板？");
            UseSystemCharPanelDescription.Text = Loc.Get("Settings.General.CharPanel.UseSystemDescription", "勾选后，点击主面板的🔣按钮将调用系统字符面板（Win+.快捷键）");
            UseSystemCharPanelWarning.Text = Loc.Get("Settings.General.CharPanel.UseSystemWarning", "仅Windows 10 Version 1709以上版本可用，其他系统请勿勾选。");

            FontSizeCardTitle.Text = Loc.Get("Settings.General.FontSize.Title", "字体大小");
            FontSizeDescription.Text = Loc.Get("Settings.General.FontSize.Description", "调整界面字体大小（需重新打开窗口生效）");
        }

        private void ApplyPanelHotkeyLocalization()
        {
            HotkeyTitleText.Text = Loc.Get("Settings.Hotkey.Title", "快捷键");
            QuickPasteCardTitle.Text = Loc.Get("Settings.Hotkey.QuickPaste.Title", "主界面快捷键");
            QuickPasteDescription.Text = Loc.Get("Settings.Hotkey.QuickPaste.Description", "为不同类型的剪贴板项配置鼠标快捷操作（修饰键 + 鼠标按键 → 操作）");
            AddGlobalHotkeyButton.Content = Loc.Get("Settings.Hotkey.Global.Add", "➕ 新增全局快捷键");
            AddQuickPasteButton.Content = Loc.Get("Settings.Hotkey.QuickPaste.Add", "➕ 新增主界面快捷键");
        }

        private void ApplyPanelQuickCommandLocalization()
        {
            QuickCommandTitleText.Text = Loc.Get("Settings.QuickCommand.Title", "快捷命令");
            QuickCommandCardTitle.Text = Loc.Get("Settings.QuickCommand.CardTitle", "文本快捷命令");
            QuickCommandDescription.Text = Loc.Get("Settings.QuickCommand.Description", "配置自定义文本处理命令：正则或字面匹配文本 → 替换或提取后粘贴。适用于文本与富文本类型。");
            AddQuickCommandButton.Content = Loc.Get("Settings.QuickCommand.Add", "➕ 新增文本快捷命令");
            QuickCommandTemplateButton.ToolTip = Loc.Get("Settings.QuickCommand.Template.Tooltip", "从模板新增");
        }

        private void ApplyPanelCaptureLocalization()
        {
            CaptureTitleText.Text = Loc.Get("Settings.Capture.Title", "捕获设置");
            CaptureCardTitle.Text = Loc.Get("Settings.Capture.CardTitle", "剪贴板捕获");
            CaptureImagesCheckBox.Content = Loc.Get("Settings.Capture.Images", "捕获图片");
            CaptureImagesDescription.Text = Loc.Get("Settings.Capture.ImagesDescription", "自动保存剪贴板中的图片内容");
            CaptureFilesCheckBox.Content = Loc.Get("Settings.Capture.Files", "捕获文件路径");
            CaptureFilesDescription.Text = Loc.Get("Settings.Capture.FilesDescription", "记录复制的文件和文件夹路径");
            RemoveDuplicatesCheckBox.Content = Loc.Get("Settings.Capture.RemoveDuplicates", "自动删除重复内容");
            RemoveDuplicatesDescription.Text = Loc.Get("Settings.Capture.RemoveDuplicatesDescription", "检测到重复内容时自动删除旧记录");
            MoveToTopCheckBox.Content = Loc.Get("Settings.Capture.MoveToTop", "粘贴后移动至首位");
            MoveToTopDescription.Text = Loc.Get("Settings.Capture.MoveToTopDescription", "点击列表项粘贴后，将该未分组记录移到列表最前面");
        }

        private void ApplyPanelHistoryLocalization()
        {
            HistoryTitleText.Text = Loc.Get("Settings.History.Title", "历史记录");
            HistoryManageCardTitle.Text = Loc.Get("Settings.History.CardTitle", "历史记录管理");
            AutoCleanupInfoText.Text = Loc.Get("Settings.History.AutoCleanupInfo", "自动删除不会删除已分组的历史记录");
            EnableAutoCleanupCheckBox.Content = Loc.Get("Settings.History.EnableAutoCleanup", "启用自动删除");
            RetentionDaysText.Text = Loc.Get("Settings.History.RetentionDays", "保留天数");
            RetentionDaysHint.Text = Loc.Get("Settings.History.RetentionDaysHint", "超过设定天数的历史记录将被自动删除");
            MaxRecordsText.Text = Loc.Get("Settings.History.MaxRecords", "最大记录数");
            MaxRecordsHint.Text = Loc.Get("Settings.History.MaxRecordsHint", "超出数量限制时自动删除最早的记录");
            KeepForeverItem.Content = Loc.Get("Settings.History.KeepForever", "永久保留");
            UnlimitedItem.Content = Loc.Get("Settings.History.Unlimited", "无限制");

            ManualCleanupCardTitle.Text = Loc.Get("Settings.History.ManualCleanup.Title", "手动删除");
            ClearAllHistoryButton.Content = Loc.Get("Settings.History.ClearAll", "删除所有历史");
            ClearUngroupedHistoryButton.Content = Loc.Get("Settings.History.ClearUngrouped", "删除未分组历史");
            ClearBeforeDaysText.Text = Loc.Get("Settings.History.ClearBeforeDays", "删除指定天数之前和指定类型的历史记录");
            ClearBeforeDaysButton.Content = Loc.Get("Settings.History.Execute", "执行");
            ClearBeforeDaysHint.Text = Loc.Get("Settings.History.ClearBeforeDaysHint", "仅删除未分组的记录，已分组的记录将被保留");

            ClearTypeAll.Content = Loc.Get("Settings.History.DeleteType.All", "全部");
            ClearTypeText.Content = Loc.Get("Settings.History.DeleteType.Text", "文本");
            ClearTypeRichText.Content = Loc.Get("Settings.History.DeleteType.RichText", "富文本");
            ClearTypeLink.Content = Loc.Get("Settings.History.DeleteType.Link", "链接");
            ClearTypeImage.Content = Loc.Get("Settings.History.DeleteType.Image", "图片");
            ClearTypeFile.Content = Loc.Get("Settings.History.DeleteType.File", "文件");
        }

        private void ApplyPanelStorageLocalization()
        {
            StorageTitleText.Text = Loc.Get("Settings.Storage.Title", "存储与备份");
            DataStorageCardTitle.Text = Loc.Get("Settings.Storage.DataStorage", "数据存储位置");
            DatabasePathText.Text = Loc.Get("Settings.Storage.DatabasePath", "数据库路径");
            BrowseDatabasePathButton.Content = Loc.Get("Common.Browse", "浏览");
            DataManageCardTitle.Text = Loc.Get("Settings.Storage.DataManagement", "数据管理");
            
            BackupDataButton.Content = Loc.Get("Settings.Storage.Backup", "备份数据");
            RestoreBackupButton.Content = Loc.Get("Settings.Storage.Restore", "恢复备份");
            ExportJsonButton.Content = Loc.Get("Settings.Storage.ExportJson", "导出JSON");
            ImportJsonButton.Content = Loc.Get("Settings.Storage.ImportJson", "导入JSON");
            ImportTextButton.Content = Loc.Get("Settings.Storage.ImportText", "导入纯文本");
            
            BackupDataHint.Text = Loc.Get("Settings.Storage.BackupHint", "备份数据：将数据库和图片打包为ZIP文件");
            RestoreBackupHint.Text = Loc.Get("Settings.Storage.RestoreHint", "恢复备份：从ZIP文件恢复数据并重启程序");
            ExportJsonHint.Text = Loc.Get("Settings.Storage.ExportJsonHint", "导出JSON：导出所有分组和剪贴板记录为JSON文件");
            ImportJsonHint.Text = Loc.Get("Settings.Storage.ImportJsonHint", "导入JSON：从JSON文件导入分组和记录，自动处理重复");
            ImportTextHint.Text = Loc.Get("Settings.Storage.ImportTextHint", "导入纯文本：从TXT文件导入文本记录（一行一条记录）");
        }

        private void ApplyPanelSearchLocalization()
        {
            SearchTitleText.Text = Loc.Get("Settings.Search.Title", "搜索引擎");
            SearchEngineCardTitle.Text = Loc.Get("Settings.Search.CardTitle", "默认搜索引擎");
            SelectSearchEngineText.Text = Loc.Get("Settings.Search.SelectEngine", "选择搜索引擎");
            CustomSearchUrlText.Text = Loc.Get("Settings.Search.CustomUrl", "自定义搜索 URL");
            KeywordPlaceholderText.Text = Loc.Get("Settings.Search.KeywordPlaceholder", "使用 %s 表示搜索关键词，如: https://www.google.com/search?q=%s");
        }

        private void ApplyPanelSystemLocalization()
        {
            SystemTitleText.Text = Loc.Get("Settings.System.Title", "系统设置");
            SystemOptionsCardTitle.Text = Loc.Get("Settings.System.Options", "系统选项");
            StartWithWindowsCheckBox.Content = Loc.Get("Settings.System.StartWithWindows", "开机自动启动");
            StartWithWindowsDescription.Text = Loc.Get("Settings.System.StartWithWindowsDescription", "系统启动时自动运行 WinVClip");
            ShowHotkeyTipCheckBox.Content = Loc.Get("Settings.System.ShowHotkeyTip", "启动时显示快捷键提示");
            ShowHotkeyTipDescription.Text = Loc.Get("Settings.System.ShowHotkeyTipDescription", "程序启动时在托盘通知中显示快捷键信息");
            EnableMonitorCheckBox.Content = Loc.Get("Settings.System.EnableMonitor", "启用剪贴板监控");
            EnableMonitorDescription.Text = Loc.Get("Settings.System.EnableMonitorDescription", "监听并记录剪贴板内容变化");
        }

        private void ApplyPanelAboutLocalization()
        {
            NavAbout.Content = Loc.Get("Settings.Nav.About", "关于");
            AboutTitleText.Text = Loc.Get("Settings.System.About.Title", "关于");
            AboutGitHubLabel.Text = Loc.Get("Settings.System.About.GitHub", "GitHub");
        }

        private void LoadAboutInfo()
        {
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(
                System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
            var versionLabel = Loc.Get("Settings.System.About.Version", "版本");
            AboutVersionText.Text = $"{versionLabel} {version}";
            ApplyPanelAboutLocalization();
        }

        private void AboutGitHubLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/adyhwang/WinVClip") { UseShellExecute = true });
            }
            catch { }
        }

        private void UpdateCustomSearchEngineGridVisibility()
        {
            if (CustomSearchEngineGrid == null)
                return;
                
            var selectedId = _settingsService.Settings.SelectedSearchEngineId;
            CustomSearchEngineGrid.Visibility = selectedId == "custom" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitializeLanguageComboBox()
        {
            if (LanguageComboBox == null)
                return;

            LocalizationService.Instance.RefreshAvailableLanguages();
            var languages = LocalizationService.Instance.AvailableLanguages;

            LanguageComboBox.Items.Clear();
            foreach (var lang in languages.Values)
            {
                var item = new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                };
                LanguageComboBox.Items.Add(item);

                if (lang.Code == _settingsService.Settings.Language)
                {
                    LanguageComboBox.SelectedItem = item;
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox == null)
                return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var selectedCode = selectedItem.Tag as string;
                _settingsService.Settings.Language = selectedCode ?? "";
            }
        }

        private void SearchEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCustomSearchEngineGridVisibility();
        }

        // 修饰键的标准顺序
        private static readonly string[] ModifierOrder = { "Ctrl", "Alt", "Shift", "Win" };

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isSettingHotkey = true;
            _tempHotkey = HotkeyTextBox.Text;
            _pressedModifiers.Clear();
            HotkeyTextBox.Text = Loc.Get("Settings.General.Hotkey.PressingKeys", "按下快捷键组合...");
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isSettingHotkey)
                return;

            e.Handled = true;

            // 处理 System 键（包括 Win 键）
            if (e.Key == Key.System)
            {
                var systemKey = e.SystemKey;
                string? modifier = GetModifierKeyName(systemKey);
                if (modifier != null)
                {
                    if (!_pressedModifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase))
                    {
                        _pressedModifiers.Add(modifier);
                        UpdateHotkeyDisplay();
                    }
                    return;
                }
            }

            // 获取修饰键名称
            string? modifierKey = GetModifierKeyName(e.Key);
            if (modifierKey != null)
            {
                // 防止重复添加相同的修饰键
                if (!_pressedModifiers.Contains(modifierKey, StringComparer.OrdinalIgnoreCase))
                {
                    _pressedModifiers.Add(modifierKey);
                    UpdateHotkeyDisplay();
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                _pressedModifiers.Clear();
                HotkeyTextBox.Text = "";
                return;
            }

            if (e.Key == Key.Back)
            {
                if (_pressedModifiers.Count > 0)
                {
                    _pressedModifiers.RemoveAt(_pressedModifiers.Count - 1);
                    UpdateHotkeyDisplay();
                }
                return;
            }

            // 处理普通按键（不是功能键如 System）
            if (e.Key != Key.System && !IsModifierKey(e.Key))
            {
                var keyName = GetKeyDisplayName(e.Key);
                var allKeys = new List<string>(_pressedModifiers);
                allKeys.Add(keyName);
                HotkeyTextBox.Text = string.Join("+", allKeys);
                _pressedModifiers.Clear();

                // 自动验证并应用快捷键
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // 短暂延迟让用户看到完整快捷键
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (HotkeyTextBox.IsFocused)
                        {
                            HotkeyTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        }
                    });
                });
            }
        }

        private string? GetModifierKeyName(Key key)
        {
            return key switch
            {
                Key.LWin or Key.RWin => "Win",
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LeftShift or Key.RightShift => "Shift",
                _ => null
            };
        }

        private bool IsModifierKey(Key key)
        {
            return key is Key.LWin or Key.RWin or
                   Key.LeftCtrl or Key.RightCtrl or
                   Key.LeftAlt or Key.RightAlt or
                   Key.LeftShift or Key.RightShift;
        }

        private string GetKeyDisplayName(Key key)
        {
            return key switch
            {
                Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
                Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
                Key.Return => "Enter",
                Key.Escape => "Esc",
                Key.Prior => "PgUp",
                Key.Next => "PgDn",
                Key.Insert => "Ins",
                Key.Delete => "Del",
                Key.Back => "Backspace",
                _ => key.ToString()
            };
        }

        private void UpdateHotkeyDisplay()
        {
            // 按标准顺序排序修饰键
            var orderedModifiers = ModifierOrder
                .Where(m => _pressedModifiers.Contains(m, StringComparer.OrdinalIgnoreCase));
            HotkeyTextBox.Text = string.Join("+", orderedModifiers);
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isSettingHotkey)
            {
                _isSettingHotkey = false;

                var newHotkey = HotkeyTextBox.Text;

                // 如果用户没有输入任何内容，恢复原来的快捷键
                if (string.IsNullOrWhiteSpace(newHotkey) || newHotkey == Loc.Get("Settings.General.Hotkey.PressingKeys", "按下快捷键组合..."))
                {
                    HotkeyTextBox.Text = _tempHotkey;
                    return;
                }

                // 标准化快捷键格式
                newHotkey = HotkeyService.NormalizeHotkey(newHotkey);

                // 使用静态方法验证格式（更高效，不需要注册）
                if (!HotkeyService.ValidateHotkey(newHotkey))
                {
                    HotkeyTextBox.Text = _tempHotkey;
                    MessageBox.Show(Loc.Get("Settings.Hotkey.InvalidFormat", "快捷键格式不正确。支持的格式：\n• 修饰键+按键（如 Ctrl+V、Alt+F1）\n• 单独的功能键 F1-F12"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查是否与当前快捷键相同
                if (newHotkey.Equals(_tempHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    HotkeyTextBox.Text = newHotkey;
                    return;
                }

                // 检查 Win+V 冲突
                if (!CheckAndHandleWinVConflict(newHotkey))
                {
                    HotkeyTextBox.Text = _tempHotkey;
                    return;
                }

                // 检查是否已被其他应用占用
                if (!IsHotkeyAvailable(newHotkey))
                {
                    HotkeyTextBox.Text = _tempHotkey;
                    MessageBox.Show(Loc.Get("Settings.Hotkey.AlreadyInUse", "该快捷键已被占用或无效"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _settingsService.Settings.Hotkey = newHotkey;
                _tempHotkey = newHotkey;
                HotkeyTextBox.Text = newHotkey;
                App.UpdateHotkey(newHotkey);
            }
        }

        private bool IsHotkeyAvailable(string hotkey)
        {
            try
            {
                // 使用临时 HotkeyService 进行验证，避免影响当前注册的快捷键
                using var tempService = new HotkeyService(IntPtr.Zero);
                return tempService.RegisterHotkey(hotkey, () => { });
            }
            catch
            {
                return false;
            }
        }

        private bool CheckAndHandleWinVConflict(string hotkey)
        {
            // 检查是否是 Win+V 快捷键
            if (!hotkey.Equals("Win+V", StringComparison.OrdinalIgnoreCase))
                return true; // 不是 Win+V，直接通过

            // 检查系统 Win+V 是否已禁用
            if (IsSystemWinVDisabled())
                return true; // 已禁用，可以使用

            // 检查是否有管理员权限
            if (!IsAdministrator())
            {
                var result = MessageBox.Show(
                    Loc.Get("Settings.Hotkey.NeedAdminForWinV", "该快捷键与系统快捷键冲突，需要管理员权限才能修改注册表并禁用系统的历史剪贴板。\n\n是否以管理员身份重启程序？"),
                    Loc.Get("Settings.Hotkey.NeedAdminTitle", "需要管理员权限"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    RestartAsAdministrator();
                }
                return false;
            }

            // 弹出提示
            var confirmResult = MessageBox.Show(
                Loc.Get("Settings.Hotkey.WinVConflict", "该快捷键与系统快捷键冲突，需要在注册表禁用系统的历史剪贴板并重启资源管理器。\n\n是否继续？"),
                Loc.Get("Settings.Hotkey.ConflictTitle", "快捷键冲突"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.OK)
                return false;

            // 禁用系统 Win+V
            return DisableSystemWinV();
        }

        private bool IsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void RestartAsAdministrator()
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas" // 请求管理员权限
                };

                System.Diagnostics.Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Settings.Admin.RestartFailed", "启动管理员权限失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsSystemWinVDisabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Clipboard");
                if (key != null)
                {
                    var value = key.GetValue("IsCloudAndHistoryFeatureAvailable");
                    if (value is int intValue)
                    {
                        return intValue == 0;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool DisableSystemWinV()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Clipboard");
                key.SetValue("IsCloudAndHistoryFeatureAvailable", 0, Microsoft.Win32.RegistryValueKind.DWord);

                // 重启资源管理器
                RestartExplorer();

                MessageBox.Show(Loc.Get("Settings.Hotkey.WinVDisabled", "系统 Win+V 已禁用，资源管理器已重启。\n\n现在可以使用 Win+V 作为快捷键了。"), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Settings.Hotkey.DisableWinVFailed", "禁用系统 Win+V 失败: {0}\n\n请以管理员身份运行程序。"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void RestartExplorer()
        {
            try
            {
                // 结束资源管理器进程
                var processes = System.Diagnostics.Process.GetProcessesByName("explorer");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                    catch
                    {
                    }
                }

                // 启动资源管理器
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });

                // 等待资源管理器启动并重建系统托盘
                System.Threading.Thread.Sleep(2000);

                // 重新创建托盘图标
                App.RecreateTrayIcon();
            }
            catch
            {
            }
        }

        private void QuickHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string hotkey)
            {
                // 检查是否与当前快捷键相同
                if (hotkey.Equals(_tempHotkey, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(Loc.Get("Settings.Hotkey.AlreadyCurrent", "该快捷键已经是当前设置。"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!HotkeyService.ValidateHotkey(hotkey))
                {
                    MessageBox.Show(Loc.Get("Settings.Hotkey.InvalidFormatShort", "快捷键格式不正确。"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!CheckAndHandleWinVConflict(hotkey))
                    return;

                if (!IsHotkeyAvailable(hotkey))
                {
                    MessageBox.Show(Loc.Get("Settings.Hotkey.AlreadyInUse", "该快捷键已被占用或无效"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _settingsService.Settings.Hotkey = hotkey;
                _tempHotkey = hotkey;
                HotkeyTextBox.Text = hotkey;
                App.UpdateHotkey(hotkey);

                MessageBox.Show(string.Format(Loc.Get("Settings.Hotkey.SetSuccess", "快捷键已设置为 {0}"), hotkey), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RestoreSystemWinV_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                var result = MessageBox.Show(
                    Loc.Get("Settings.Hotkey.RestoreWinVNeedAdmin", "还原系统 Win+V 需要管理员权限才能修改注册表。\n\n是否以管理员身份重启程序？"),
                    Loc.Get("Settings.Hotkey.NeedAdminTitle", "需要管理员权限"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    RestartAsAdministrator();
                }
                return;
            }

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Clipboard");
                key.SetValue("IsCloudAndHistoryFeatureAvailable", 1, Microsoft.Win32.RegistryValueKind.DWord);

                RestartExplorer();

                MessageBox.Show(Loc.Get("Settings.Hotkey.WinVRestored", "系统 Win+V 已还原，资源管理器已重启。\n\n现在可以使用系统历史剪贴板功能了。"), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Settings.Hotkey.RestoreWinVFailed", "还原系统 Win+V 失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RequestAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (IsAdministrator())
            {
                MessageBox.Show(Loc.Get("Settings.Admin.AlreadyAdmin", "当前已以管理员权限运行。"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                Loc.Get("Settings.Admin.NeedRestart", "获取管理员权限需要重启程序。\n\n是否以管理员身份重启？"),
                Loc.Get("Settings.Admin.GetAdminTitle", "获取管理员权限"),
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                RestartAsAdministrator();
            }
        }

        private void UpdateAdminButtonState()
        {
            if (RequestAdminButton == null) return;

            if (IsAdministrator())
            {
                RequestAdminButton.Content = Loc.Get("Settings.Admin.HasAdmin", "已获取管理员权限");
                RequestAdminButton.IsEnabled = false;
            }
        }

        private void BrowseDatabasePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Database", "数据库文件 (*.db)|*.db") + "|" + Loc.Get("FileDialog.Filter.AllFiles", "所有文件 (*.*)|*.*"),
                DefaultExt = ".db",
                FileName = "clipboard_history.db"
            };
            
            if (dialog.ShowDialog() == true)
            {
                _settingsService.Settings.DatabasePath = dialog.FileName;
            }
        }

        private void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Zip", "ZIP压缩文件 (*.zip)|*.zip"),
                DefaultExt = ".zip",
                FileName = $"WinVClip_backup_{DateTime.Now:yyyy_MM_dd}.zip"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var dbPath = _settingsService.Settings.DatabasePath;
                    if (!File.Exists(dbPath))
                    {
                        MessageBox.Show(Loc.Get("Settings.Storage.DatabaseNotFound", "数据库文件不存在"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var dbDir = System.IO.Path.GetDirectoryName(dbPath);
                    var imagesPath = System.IO.Path.Combine(dbDir, "images");
                    var tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                    var tempBackupPath = System.IO.Path.Combine(tempDir, $"Backup_{Guid.NewGuid():N}");
                    
                    Directory.CreateDirectory(tempBackupPath);

                    try
                    {
                        var tempDbPath = System.IO.Path.Combine(tempBackupPath, "clipboard_history.db");
                        File.Copy(dbPath, tempDbPath, true);

                        var tempImagesPath = System.IO.Path.Combine(tempBackupPath, "images");
                        if (Directory.Exists(imagesPath))
                        {
                            CopyDirectory(imagesPath, tempImagesPath);
                        }

                        using (var zip = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create))
                        {
                            zip.CreateEntryFromFile(tempDbPath, "clipboard_history.db");

                            if (Directory.Exists(tempImagesPath))
                            {
                                var imageFiles = Directory.GetFiles(tempImagesPath, "*.*", SearchOption.AllDirectories);
                                foreach (var imageFile in imageFiles)
                                {
                                    var relativePath = imageFile.Substring(tempImagesPath.Length + 1);
                                    zip.CreateEntryFromFile(imageFile, System.IO.Path.Combine("images", relativePath));
                                }
                            }
                        }

                        if (Directory.Exists(tempBackupPath))
                        {
                            Directory.Delete(tempBackupPath, true);
                        }

                        MessageBox.Show(Loc.Get("Settings.Storage.BackupSuccess", "备份成功！"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch
                    {
                        if (Directory.Exists(tempBackupPath))
                        {
                            Directory.Delete(tempBackupPath, true);
                        }
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Loc.Get("Settings.Storage.BackupFailed", "备份失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Loc.Get("Settings.Storage.RestoreConfirm", "恢复备份将覆盖当前所有数据，程序将自动重启。是否继续？"),
                Loc.Get("Settings.Storage.RestoreConfirmTitle", "确认恢复"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Zip", "ZIP压缩文件 (*.zip)|*.zip"),
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var dbPath = _settingsService.Settings.DatabasePath;
                var dbDir = System.IO.Path.GetDirectoryName(dbPath);
                var imagesPath = System.IO.Path.Combine(dbDir, "images");
                var tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                var tempExtractPath = System.IO.Path.Combine(tempDir, $"Restore_{Guid.NewGuid():N}");

                Directory.CreateDirectory(tempExtractPath);

                try
                {
                    ZipFile.ExtractToDirectory(dialog.FileName, tempExtractPath);

                    var extractedDbPath = System.IO.Path.Combine(tempExtractPath, "clipboard_history.db");
                    if (!File.Exists(extractedDbPath))
                    {
                        MessageBox.Show(Loc.Get("Settings.Storage.DatabaseNotFoundInBackup", "备份文件中未找到数据库"), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                        Directory.Delete(tempExtractPath, true);
                        return;
                    }

                    var extractedImagesPath = System.IO.Path.Combine(tempExtractPath, "images");

                    App.DatabaseService.Dispose();

                    try
                    {
                        File.Copy(extractedDbPath, dbPath, true);

                        if (Directory.Exists(imagesPath))
                        {
                            Directory.Delete(imagesPath, true);
                        }

                        if (Directory.Exists(extractedImagesPath))
                        {
                            CopyDirectory(extractedImagesPath, imagesPath);
                        }

                        try
                        {
                            Directory.Delete(tempExtractPath, true);
                        }
                        catch { }

                        MessageBox.Show(Loc.Get("Settings.Storage.RestoreSuccess", "恢复成功！程序将自动重启。"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);

                        Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Loc.Get("Settings.Storage.RestoreFailed", "恢复失败: {0}\n请手动重启程序。"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                        Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    if (Directory.Exists(tempExtractPath))
                        Directory.Delete(tempExtractPath, true);
                    MessageBox.Show(string.Format(Loc.Get("Settings.Storage.ExtractFailed", "解压失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Settings.Storage.RestoreFailed", "恢复失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Text", "文本文件 (*.txt)|*.txt"),
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                // 创建进度窗口
                var progressWindow = new Window
                {
                    Title = Loc.Get("Import.Title", "导入数据"),
                    Width = 350,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };
                var statusText = new TextBlock
                {
                    Text = Loc.Get("Import.ReadingFile", "正在读取文件..."),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var progressBar = new ProgressBar
                {
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };
                var detailText = new TextBlock
                {
                    Text = "",
                    Margin = new Thickness(0, 10, 0, 0),
                    Foreground = System.Windows.Media.Brushes.Gray
                };

                stackPanel.Children.Add(statusText);
                stackPanel.Children.Add(progressBar);
                stackPanel.Children.Add(detailText);
                progressWindow.Content = stackPanel;

                // 异步执行导入
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var lines = System.IO.File.ReadAllLines(dialog.FileName);
                        var totalLines = lines.Length;
                        var importCount = 0;
                        var skippedCount = 0;
                        var deletedCount = 0;

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = Loc.Get("Import.AnalyzingFile", "正在分析文件...");
                            progressBar.Maximum = 1;
                            progressBar.Value = 0;
                            detailText.Text = string.Format(Loc.Get("Import.FileTotalLines", "文件共 {0} 行"), totalLines);
                            progressWindow.Show();
                        });

                        var uniqueContents = new HashSet<string>();
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedLine))
                            {
                                uniqueContents.Add(trimmedLine);
                            }
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = Loc.Get("Import.CheckingDuplicates", "正在检查重复数据...");
                            detailText.Text = string.Format(Loc.Get("Import.UniqueDataCount", "共 {0} 条不重复数据"), uniqueContents.Count);
                        });

                        var existingContents = App.DatabaseService.GetExistingTextContents(uniqueContents);

                        var groupedContents = new HashSet<string>();
                        var ungroupedContents = new HashSet<string>();

                        foreach (var kvp in existingContents)
                        {
                            if (kvp.Value)
                                groupedContents.Add(kvp.Key);
                            else
                                ungroupedContents.Add(kvp.Key);
                        }

                        skippedCount = groupedContents.Count;

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = Loc.Get("Import.CleaningDuplicates", "正在删除未分组重复数据...");
                            detailText.Text = string.Format(Loc.Get("Import.GroupedCount", "已分组: {0} 条"), groupedContents.Count) + "，" + string.Format(Loc.Get("Import.UngroupedCount", "未分组: {0} 条"), ungroupedContents.Count);
                        });

                        if (ungroupedContents.Count > 0)
                        {
                            deletedCount = App.DatabaseService.DeleteUngroupedDuplicatesBatch(ungroupedContents);
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = Loc.Get("Import.PreparingImport", "正在准备导入数据...");
                            detailText.Text = string.Format(Loc.Get("Import.CleanedCount", "已删除 {0} 条，跳过 {1} 条"), deletedCount, skippedCount);
                        });

                        var itemsToImport = new List<ClipboardItem>();
                        var baseTime = DateTime.Now;

                        for (int i = lines.Length - 1; i >= 0; i--)
                        {
                            var trimmedLine = lines[i].Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedLine) && !groupedContents.Contains(trimmedLine))
                            {
                                itemsToImport.Add(new ClipboardItem
                                {
                                    Type = ClipboardType.Text,
                                    Content = trimmedLine,
                                    CreatedAt = baseTime.AddSeconds(itemsToImport.Count),
                                    PreviewText = trimmedLine.Length > 100 ? trimmedLine.Substring(0, 100) : trimmedLine
                                });
                            }
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = Loc.Get("Import.Importing", "正在导入数据...") + " " + string.Format(Loc.Get("Import.Progress", "{0}/{1}"), 0, itemsToImport.Count);
                            progressBar.Value = 0;
                            progressBar.Maximum = itemsToImport.Count;
                        });

                        if (itemsToImport.Count > 0)
                        {
                            importCount = App.DatabaseService.InsertItemsBatch(itemsToImport);
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow.Close();
                            var message = string.Format(Loc.Get("Settings.Storage.ImportSuccess", "导入成功！共导入 {0} 条记录。"), importCount);
                            if (deletedCount > 0)
                                message += string.Format(Loc.Get("Settings.Storage.ImportCleaned", "\n已删除 {0} 条未分组重复数据。"), deletedCount);
                            if (skippedCount > 0)
                                message += string.Format(Loc.Get("Settings.Storage.ImportSkipped", "\n已跳过 {0} 条已分组重复数据（保留）。"), skippedCount);
                            MessageBox.Show(message, Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow.Close();
                            MessageBox.Show(string.Format(Loc.Get("Settings.Storage.ImportFailed", "导入失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Json", "JSON文件 (*.json)|*.json"),
                DefaultExt = ".json",
                FileName = $"clipboard_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var items = App.DatabaseService.GetAllItems();
                    var groups = App.DatabaseService.GetAllGroups();

                    var exportItems = new List<object>();
                    foreach (var item in items)
                    {
                        exportItems.Add(new
                        {
                            Type = item.Type.ToString(),
                            Content = item.Content,
                            RichContent = item.RichContent,
                            RichFormat = item.RichFormat,
                            CsvContent = item.CsvContent,
                            ImagePath = item.ImagePath,
                            ImageHash = item.ImageHash,
                            FilePaths = item.FilePaths,
                            CreatedAt = item.CreatedAt,
                            PreviewText = item.PreviewText,
                            GroupId = item.GroupId,
                            GroupName = item.GroupName
                        });
                    }

                    var exportGroups = new List<object>();
                    foreach (var group in groups)
                    {
                        exportGroups.Add(new
                        {
                            Id = group.Id,
                            Name = group.Name,
                            CreatedAt = group.CreatedAt
                        });
                    }

                    var exportData = new
                    {
                        Groups = exportGroups,
                        Items = exportItems
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    System.IO.File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                    MessageBox.Show(string.Format(Loc.Get("Settings.Storage.ExportJsonSuccess", "导出成功！共导出 {0} 条记录。"), exportItems.Count), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Loc.Get("Settings.Storage.ExportFailed", "导出失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportJson_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Loc.Get("FileDialog.Filter.Json", "JSON文件 (*.json)|*.json"),
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                    var root = jsonDoc.RootElement;

                    if (root.ValueKind != System.Text.Json.JsonValueKind.Object || 
                        !root.TryGetProperty("Items", out var itemsProp))
                    {
                        MessageBox.Show(Loc.Get("Settings.Storage.NoDataToImport", "文件中没有可导入的数据。"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var importItems = itemsProp.EnumerateArray().ToList();
                    List<System.Text.Json.JsonElement>? importGroups = null;
                    if (root.TryGetProperty("Groups", out var groupsProp))
                    {
                        importGroups = groupsProp.EnumerateArray().ToList();
                    }

                    if (importItems == null || importItems.Count == 0)
                    {
                        MessageBox.Show(Loc.Get("Settings.Storage.NoDataToImport", "文件中没有可导入的数据。"), Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var existingGroups = App.DatabaseService.GetAllGroups();
                    var groupNameToId = existingGroups.ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);
                    var importedGroupIdMap = new Dictionary<long, long>();

                    if (importGroups != null)
                    {
                        foreach (var group in importGroups)
                        {
                            var groupName = group.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? nameProp.GetString() : null;
                            var originalId = group.TryGetProperty("Id", out var idProp) && idProp.ValueKind == System.Text.Json.JsonValueKind.Number 
                                ? idProp.GetInt64() : 0;
                            
                            if (!string.IsNullOrWhiteSpace(groupName))
                            {
                                if (!groupNameToId.ContainsKey(groupName))
                                {
                                    var createdAt = group.TryGetProperty("CreatedAt", out var createdProp) && createdProp.ValueKind != System.Text.Json.JsonValueKind.Null
                                        ? createdProp.GetDateTime() 
                                        : DateTime.Now;
                                    var groupIcon = group.TryGetProperty("Icon", out var iconProp) && iconProp.ValueKind != System.Text.Json.JsonValueKind.Null
                                        ? iconProp.GetString() ?? "⭐"
                                        : "⭐";
                                    var newId = App.DatabaseService.InsertGroup(groupName, groupIcon, createdAt);
                                    groupNameToId[groupName] = newId;
                                    if (originalId > 0)
                                        importedGroupIdMap[originalId] = newId;
                                }
                                else if (originalId > 0)
                                {
                                    importedGroupIdMap[originalId] = groupNameToId[groupName];
                                }
                            }
                        }
                    }

                    var textContents = new HashSet<string>();
                    var richTextContents = new HashSet<string>();
                    var fileListSignatures = new HashSet<string>();
                    var imageHashes = new HashSet<string>();
                    var baseTime = DateTime.Now;

                    foreach (var item in importItems)
                    {
                        var typeStr = item.TryGetProperty("Type", out var typeProp) && typeProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                            ? typeProp.GetString() : "Text";
                        
                        if (typeStr == "Text")
                        {
                            var content = item.TryGetProperty("Content", out var contentProp) && contentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? contentProp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                textContents.Add(content);
                            }
                        }
                        else if (typeStr == "RichText")
                        {
                            var richContent = item.TryGetProperty("RichContent", out var richContentProp) && richContentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? richContentProp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(richContent))
                            {
                                richTextContents.Add(richContent);
                            }
                        }
                        else if (typeStr == "FileList")
                        {
                            if (item.TryGetProperty("FilePaths", out var filePathsProp) && filePathsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var filePaths = filePathsProp.EnumerateArray()
                                    .Where(p => p.ValueKind != System.Text.Json.JsonValueKind.Null)
                                    .Select(p => p.GetString())
                                    .Where(p => p != null)
                                    .ToList()!;
                                if (filePaths.Count > 0)
                                {
                                    var signature = string.Join("|", filePaths.OrderBy(p => p));
                                    fileListSignatures.Add(signature);
                                }
                            }
                        }
                        else if (typeStr == "Image")
                        {
                            var imageHash = item.TryGetProperty("ImageHash", out var hashProp) && hashProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? hashProp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(imageHash))
                            {
                                imageHashes.Add(imageHash);
                            }
                        }
                    }

                    var existingTextContents = App.DatabaseService.GetExistingTextContents(textContents);
                    var groupedTextContents = new HashSet<string>();
                    var ungroupedTextContents = new HashSet<string>();
                    foreach (var kvp in existingTextContents)
                    {
                        if (kvp.Value)
                            groupedTextContents.Add(kvp.Key);
                        else
                            ungroupedTextContents.Add(kvp.Key);
                    }

                    var existingRichTextContents = App.DatabaseService.GetExistingRichTextContents(richTextContents);
                    var groupedRichTextContents = new HashSet<string>();
                    var ungroupedRichTextContents = new HashSet<string>();
                    foreach (var kvp in existingRichTextContents)
                    {
                        if (kvp.Value)
                            groupedRichTextContents.Add(kvp.Key);
                        else
                            ungroupedRichTextContents.Add(kvp.Key);
                    }

                    var existingFileListSignatures = App.DatabaseService.GetExistingFileListSignatures(fileListSignatures);
                    var groupedFileListSignatures = new HashSet<string>();
                    var ungroupedFileListSignatures = new HashSet<string>();
                    foreach (var kvp in existingFileListSignatures)
                    {
                        if (kvp.Value)
                            groupedFileListSignatures.Add(kvp.Key);
                        else
                            ungroupedFileListSignatures.Add(kvp.Key);
                    }

                    var existingImageHashes = App.DatabaseService.GetExistingImageHashes(imageHashes);
                    var groupedImageHashes = new HashSet<string>();
                    var ungroupedImageHashes = new HashSet<string>();
                    foreach (var kvp in existingImageHashes)
                    {
                        if (kvp.Value)
                            groupedImageHashes.Add(kvp.Key);
                        else
                            ungroupedImageHashes.Add(kvp.Key);
                    }

                    var deletedCount = 0;
                    if (ungroupedTextContents.Count > 0)
                        deletedCount += App.DatabaseService.DeleteUngroupedDuplicatesBatch(ungroupedTextContents);
                    if (ungroupedRichTextContents.Count > 0)
                        deletedCount += App.DatabaseService.DeleteUngroupedRichTextDuplicatesBatch(ungroupedRichTextContents);
                    if (ungroupedFileListSignatures.Count > 0)
                        deletedCount += App.DatabaseService.DeleteUngroupedFileListDuplicatesBatch(ungroupedFileListSignatures);
                    if (ungroupedImageHashes.Count > 0)
                        deletedCount += App.DatabaseService.DeleteUngroupedImageDuplicatesBatch(ungroupedImageHashes);

                    var itemsToImport = new List<ClipboardItem>();
                    var skippedCount = groupedTextContents.Count + groupedRichTextContents.Count + groupedFileListSignatures.Count + groupedImageHashes.Count;
                    var addedTextContents = new HashSet<string>();
                    var addedRichTextContents = new HashSet<string>();
                    var addedFileListSignatures = new HashSet<string>();
                    var addedImageHashes = new HashSet<string>();

                    foreach (var item in importItems)
                    {
                        var typeStr = item.TryGetProperty("Type", out var typeProp) && typeProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                            ? typeProp.GetString() : "Text";
                        var createdAt = item.TryGetProperty("CreatedAt", out var createdAtProp) && createdAtProp.ValueKind != System.Text.Json.JsonValueKind.Null
                            ? createdAtProp.GetDateTime() 
                            : baseTime.AddSeconds(itemsToImport.Count);

                        long? groupId = null;
                        if (item.TryGetProperty("GroupId", out var groupIdProp) && groupIdProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            var originalGroupId = groupIdProp.GetInt64();
                            if (originalGroupId > 0 && importedGroupIdMap.TryGetValue(originalGroupId, out var newGroupId))
                            {
                                groupId = newGroupId;
                            }
                        }
                        if (groupId == null && item.TryGetProperty("GroupName", out var groupNameProp) && groupNameProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            var groupName = groupNameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(groupName) && groupNameToId.TryGetValue(groupName, out var existingGroupId))
                            {
                                groupId = existingGroupId;
                            }
                        }

                        if (typeStr == "Text")
                        {
                            var content = item.TryGetProperty("Content", out var contentProp) && contentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? contentProp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(content) && !groupedTextContents.Contains(content) && !addedTextContents.Contains(content))
                            {
                                addedTextContents.Add(content);
                                var previewText = item.TryGetProperty("PreviewText", out var previewProp) && previewProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                    ? previewProp.GetString() : null;
                                itemsToImport.Add(new ClipboardItem
                                {
                                    Type = ClipboardType.Text,
                                    Content = content,
                                    CreatedAt = createdAt,
                                    PreviewText = !string.IsNullOrWhiteSpace(previewText) ? previewText : (content.Length > 100 ? content.Substring(0, 100) : content),
                                    GroupId = groupId
                                });
                            }
                        }
                        else if (typeStr == "RichText")
                        {
                            var content = item.TryGetProperty("Content", out var contentProp) && contentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? contentProp.GetString() : "";
                            var richContent = item.TryGetProperty("RichContent", out var richContentProp) && richContentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? richContentProp.GetString() : null;
                            var richFormat = item.TryGetProperty("RichFormat", out var formatProp) && formatProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? formatProp.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(richContent) && !groupedRichTextContents.Contains(richContent) && !addedRichTextContents.Contains(richContent))
                            {
                                addedRichTextContents.Add(richContent);
                                var previewText = item.TryGetProperty("PreviewText", out var previewProp) && previewProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                    ? previewProp.GetString() : null;
                                itemsToImport.Add(new ClipboardItem
                                {
                                    Type = ClipboardType.RichText,
                                    Content = content ?? "",
                                    RichContent = richContent,
                                    RichFormat = richFormat,
                                    CreatedAt = createdAt,
                                    PreviewText = !string.IsNullOrWhiteSpace(previewText) ? previewText : (!string.IsNullOrWhiteSpace(content) && content.Length > 100 ? content.Substring(0, 100) : content ?? ""),
                                    GroupId = groupId
                                });
                            }
                        }
                        else if (typeStr == "FileList")
                        {
                            if (item.TryGetProperty("FilePaths", out var filePathsProp) && filePathsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var filePaths = filePathsProp.EnumerateArray()
                                    .Where(p => p.ValueKind != System.Text.Json.JsonValueKind.Null)
                                    .Select(p => p.GetString())
                                    .Where(p => p != null)
                                    .ToList()!;

                                if (filePaths.Count > 0)
                                {
                                    var signature = string.Join("|", filePaths.OrderBy(p => p));
                                    if (!groupedFileListSignatures.Contains(signature) && !addedFileListSignatures.Contains(signature))
                                    {
                                        addedFileListSignatures.Add(signature);
                                        var previewText = item.TryGetProperty("PreviewText", out var previewProp) && previewProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                            ? previewProp.GetString() : null;
                                        itemsToImport.Add(new ClipboardItem
                                        {
                                            Type = ClipboardType.FileList,
                                            FilePaths = filePaths,
                                            CreatedAt = createdAt,
                                            PreviewText = !string.IsNullOrWhiteSpace(previewText) ? previewText : (string.Join("\n", filePaths.Take(3)) + (filePaths.Count > 3 ? $"\n...共{filePaths.Count}个文件" : "")),
                                            GroupId = groupId
                                        });
                                    }
                                }
                            }
                        }
                        else if (typeStr == "Image")
                        {
                            var imagePath = item.TryGetProperty("ImagePath", out var imagePathProp) && imagePathProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? imagePathProp.GetString() : null;
                            var imageHash = item.TryGetProperty("ImageHash", out var hashProp) && hashProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? hashProp.GetString() : null;
                            var content = item.TryGetProperty("Content", out var contentProp) && contentProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                ? contentProp.GetString() : "";

                            if (!string.IsNullOrWhiteSpace(imageHash) && !groupedImageHashes.Contains(imageHash) && !addedImageHashes.Contains(imageHash))
                            {
                                addedImageHashes.Add(imageHash);
                                var previewText = item.TryGetProperty("PreviewText", out var previewProp) && previewProp.ValueKind != System.Text.Json.JsonValueKind.Null 
                                    ? previewProp.GetString() : null;
                                itemsToImport.Add(new ClipboardItem
                                {
                                    Type = ClipboardType.Image,
                                    Content = content ?? "",
                                    ImagePath = imagePath,
                                    ImageHash = imageHash,
                                    CreatedAt = createdAt,
                                    PreviewText = !string.IsNullOrWhiteSpace(previewText) ? previewText : (content ?? ""),
                                    GroupId = groupId
                                });
                            }
                        }
                    }

                    var importCount = App.DatabaseService.InsertItemsBatch(itemsToImport);
                    var message = string.Format(Loc.Get("Settings.Storage.ImportJsonSuccess", "导入成功！共导入 {0} 条记录。"), importCount);
                    if (deletedCount > 0)
                        message += string.Format(Loc.Get("Settings.Storage.ImportCleaned", "\n已删除 {0} 条未分组重复数据。"), deletedCount);
                    if (skippedCount > 0)
                        message += string.Format(Loc.Get("Settings.Storage.ImportSkipped", "\n已跳过 {0} 条已分组重复数据（保留）。"), skippedCount);
                    MessageBox.Show(message, Loc.Get("Message.Info", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Loc.Get("Settings.Storage.ImportFailed", "导入失败: {0}"), ex.Message), Loc.Get("Message.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            var monitoringChanged = _settingsService.Settings.MonitorEnabled != _originalSettings.MonitorEnabled;
            var cleanupChanged = _settingsService.Settings.EnableAutoCleanup != _originalSettings.EnableAutoCleanup ||
                                _settingsService.Settings.RetentionDays != _originalSettings.RetentionDays;
            var themeChanged = _settingsService.Settings.Theme != _originalSettings.Theme;
            var languageChanged = false;
            
            if (LanguageComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var selectedCode = selectedItem.Tag as string;
                languageChanged = selectedCode != _originalSettings.Language;
                _settingsService.Settings.Language = selectedCode ?? "";
            }

            var currentStartWithWindows = StartWithWindowsCheckBox?.IsChecked ?? false;
            var startWithWindowsChanged = currentStartWithWindows != _originalStartWithWindows;

            CollectQuickPasteEntries();
            CollectQuickCommandEntries();
            CollectGlobalHotkeyEntries();
            _settingsService.SaveSettings();

            if (monitoringChanged)
            {
                App.UpdateMonitoring(_settingsService.Settings.MonitorEnabled);
            }

            if (cleanupChanged)
            {
                App.UpdateCleanup(_settingsService.Settings.EnableAutoCleanup,
                               _settingsService.Settings.RetentionDays);
            }

            if (themeChanged)
            {
                Services.ThemeService.Instance.UpdateTheme(_settingsService.Settings.Theme);
            }

            if (startWithWindowsChanged)
            {
                _settingsService.SetStartupRegistry(currentStartWithWindows);
            }

            if (languageChanged)
            {
                LocalizationService.Instance.LoadLanguage(_settingsService.Settings.Language);
            }

            // 全局快捷键 / 显示隐藏热键变更后统一重新注册全部热键
            App.RegisterAllHotkeys();
            SyncGlobalHotkeyEntrySnapshots();
            _originalSettings.GlobalHotkeys = _settingsService.Settings.GlobalHotkeys?
                .Select(g => g.Clone()).ToList() ?? new List<GlobalHotkey>();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var hotkeyChanged = _settingsService.Settings.Hotkey != _originalSettings.Hotkey;
            var cleanupChanged = _settingsService.Settings.EnableAutoCleanup != _originalSettings.EnableAutoCleanup ||
                                _settingsService.Settings.RetentionDays != _originalSettings.RetentionDays;
            var themeChanged = _settingsService.Settings.Theme != _originalSettings.Theme;
            var currentStartWithWindows = StartWithWindowsCheckBox?.IsChecked ?? false;
            var startWithWindowsChanged = currentStartWithWindows != _originalStartWithWindows;

            _settingsService.Settings.Hotkey = _originalSettings.Hotkey;
            _settingsService.Settings.CaptureImages = _originalSettings.CaptureImages;
            _settingsService.Settings.CaptureFiles = _originalSettings.CaptureFiles;
            _settingsService.Settings.RemoveDuplicates = _originalSettings.RemoveDuplicates;
            _settingsService.Settings.DatabasePath = _originalSettings.DatabasePath;
            _settingsService.Settings.EnableAutoCleanup = _originalSettings.EnableAutoCleanup;
            _settingsService.Settings.RetentionDays = _originalSettings.RetentionDays;
            _settingsService.Settings.MonitorEnabled = _originalSettings.MonitorEnabled;
            _settingsService.Settings.Theme = _originalSettings.Theme;
            _settingsService.Settings.MaxHistoryItems = _originalSettings.MaxHistoryItems;
            _settingsService.Settings.MoveToTopAfterPaste = _originalSettings.MoveToTopAfterPaste;
            _settingsService.Settings.PasteShortcutMode = _originalSettings.PasteShortcutMode;
            _settingsService.Settings.Language = _originalSettings.Language;
            _settingsService.Settings.FontSize = _originalSettings.FontSize;
            _settingsService.Settings.ShowHotkeyTipOnStartup = _originalSettings.ShowHotkeyTipOnStartup;
            _settingsService.Settings.QuickPasteShortcuts = _originalSettings.QuickPasteShortcuts?
                .Select(s => s.Clone()).ToList() ?? new List<QuickPasteShortcut>();
            _settingsService.Settings.QuickCommands = _originalSettings.QuickCommands?
                .Select(s => s.Clone()).ToList() ?? new List<QuickCommand>();
            _settingsService.Settings.GlobalHotkeys = _originalSettings.GlobalHotkeys?
                .Select(g => g.Clone()).ToList() ?? new List<GlobalHotkey>();
            LoadQuickCommandEntries();
            LoadGlobalHotkeyEntries();
            ValidateAllEntries();

            if (hotkeyChanged)
            {
                App.UpdateHotkey(_originalSettings.Hotkey);
            }

            if (cleanupChanged)
            {
                App.UpdateCleanup(_originalSettings.EnableAutoCleanup,
                               _originalSettings.RetentionDays);
            }

            if (themeChanged)
            {
                Services.ThemeService.Instance.UpdateTheme(_originalSettings.Theme);
            }

            if (startWithWindowsChanged)
            {
                _settingsService.SetStartupRegistry(_originalStartWithWindows);
            }

            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var hotkeyChanged = _settingsService.Settings.Hotkey != _originalSettings.Hotkey;
            var monitoringChanged = _settingsService.Settings.MonitorEnabled != _originalSettings.MonitorEnabled;
            var cleanupChanged = _settingsService.Settings.EnableAutoCleanup != _originalSettings.EnableAutoCleanup ||
                                _settingsService.Settings.RetentionDays != _originalSettings.RetentionDays;
            var themeChanged = _settingsService.Settings.Theme != _originalSettings.Theme;
            var languageChanged = _settingsService.Settings.Language != _originalSettings.Language;
            // 从CheckBox获取当前开机启动状态
            var currentStartWithWindows = StartWithWindowsCheckBox?.IsChecked ?? false;
            var startWithWindowsChanged = currentStartWithWindows != _originalStartWithWindows;

            CollectQuickPasteEntries();
            CollectQuickCommandEntries();
            CollectGlobalHotkeyEntries();
            _settingsService.SaveSettings();

            // 更新原始设置，使应用后的设置成为新的基准
            _originalSettings.Hotkey = _settingsService.Settings.Hotkey;
            _originalSettings.CaptureImages = _settingsService.Settings.CaptureImages;
            _originalSettings.CaptureFiles = _settingsService.Settings.CaptureFiles;
            _originalSettings.RemoveDuplicates = _settingsService.Settings.RemoveDuplicates;
            _originalSettings.DatabasePath = _settingsService.Settings.DatabasePath;
            _originalSettings.EnableAutoCleanup = _settingsService.Settings.EnableAutoCleanup;
            _originalSettings.RetentionDays = _settingsService.Settings.RetentionDays;
            _originalStartWithWindows = currentStartWithWindows;
            _originalSettings.MonitorEnabled = _settingsService.Settings.MonitorEnabled;
            _originalSettings.Theme = _settingsService.Settings.Theme;
            _originalSettings.SelectedSearchEngineId = _settingsService.Settings.SelectedSearchEngineId;
            _originalSettings.CustomSearchEngineUrl = _settingsService.Settings.CustomSearchEngineUrl;
            _originalSettings.MaxHistoryItems = _settingsService.Settings.MaxHistoryItems;
            _originalSettings.FontSize = _settingsService.Settings.FontSize;
            _originalSettings.ShowHotkeyTipOnStartup = _settingsService.Settings.ShowHotkeyTipOnStartup;
            _originalSettings.QuickPasteShortcuts = _settingsService.Settings.QuickPasteShortcuts?
                .Select(s => s.Clone()).ToList() ?? new List<QuickPasteShortcut>();
            _originalSettings.QuickCommands = _settingsService.Settings.QuickCommands?
                .Select(s => s.Clone()).ToList() ?? new List<QuickCommand>();
            _originalSettings.GlobalHotkeys = _settingsService.Settings.GlobalHotkeys?
                .Select(g => g.Clone()).ToList() ?? new List<GlobalHotkey>();
            // 同步每条条目的「已保存快照」为当前值，供「重置」按钮使用
            SyncQuickPasteEntrySnapshots();
            SyncQuickCommandEntrySnapshots();
            SyncGlobalHotkeyEntrySnapshots();

            // 全局快捷键 / 显示隐藏热键变更后统一重新注册全部热键
            App.RegisterAllHotkeys();

            if (monitoringChanged)
            {
                App.UpdateMonitoring(_settingsService.Settings.MonitorEnabled);
            }

            if (cleanupChanged)
            {
                App.UpdateCleanup(_settingsService.Settings.EnableAutoCleanup,
                               _settingsService.Settings.RetentionDays);
            }

            if (themeChanged)
            {
                Services.ThemeService.Instance.UpdateTheme(_settingsService.Settings.Theme);
            }

            if (startWithWindowsChanged)
            {
                _settingsService.SetStartupRegistry(currentStartWithWindows);
            }

            if (languageChanged)
            {
                LocalizationService.Instance.LoadLanguage(_settingsService.Settings.Language);
                _originalSettings.Language = _settingsService.Settings.Language;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void NavRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is string tag)
            {
                // 隐藏所有面板
                PanelGeneral.Visibility = Visibility.Collapsed;
                PanelHotkey.Visibility = Visibility.Collapsed;
                PanelQuickCommand.Visibility = Visibility.Collapsed;
                PanelCapture.Visibility = Visibility.Collapsed;
                PanelHistory.Visibility = Visibility.Collapsed;
                PanelStorage.Visibility = Visibility.Collapsed;
                PanelSearch.Visibility = Visibility.Collapsed;
                PanelSystem.Visibility = Visibility.Collapsed;
                PanelAbout.Visibility = Visibility.Collapsed;

                // 显示选中的面板
                switch (tag)
                {
                    case "General":
                        PanelGeneral.Visibility = Visibility.Visible;
                        break;
                    case "Hotkey":
                        PanelHotkey.Visibility = Visibility.Visible;
                        break;
                    case "QuickCommand":
                        PanelQuickCommand.Visibility = Visibility.Visible;
                        break;
                    case "Capture":
                        PanelCapture.Visibility = Visibility.Visible;
                        break;
                    case "History":
                        PanelHistory.Visibility = Visibility.Visible;
                        break;
                    case "Storage":
                        PanelStorage.Visibility = Visibility.Visible;
                        break;
                    case "Search":
                        PanelSearch.Visibility = Visibility.Visible;
                        break;
                    case "System":
                        PanelSystem.Visibility = Visibility.Visible;
                        break;
                    case "About":
                        PanelAbout.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void ClearBeforeDaysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ClearBeforeDaysValueText != null)
            {
                ClearBeforeDaysValueText.Text = ((int)e.NewValue).ToString();
            }
        }

        private void ClearAllHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Loc.Get("MainWindow.Message.ConfirmClearAll", "确定要删除所有剪贴板历史记录吗？\n\n此操作将删除所有记录（包括已分组的记录）。"),
                Loc.Get("Message.Confirm", "确认"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                App.DatabaseService.ClearHistory();
                App.GetMainWindow()?.ViewModel?.LoadItems();
                MessageBox.Show(Loc.Get("Message.ClearSuccess", "删除成功"), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearUngroupedHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Loc.Get("MainWindow.Message.ConfirmClearUngrouped", "确定要删除所有未分组的剪贴板历史记录吗？\n\n已分组的记录将保留。"),
                Loc.Get("Message.Confirm", "确认"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                App.DatabaseService.ClearUngroupedHistory();
                App.GetMainWindow()?.ViewModel?.LoadItems();
                MessageBox.Show(Loc.Get("Message.ClearSuccess", "删除成功"), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearBeforeDays_Click(object sender, RoutedEventArgs e)
        {
            var days = (int)ClearBeforeDaysSlider.Value;
            var cutoffDate = days > 0 ? DateTime.Now.AddDays(-days) : DateTime.MinValue;

            int? typeFilter = null;
            string typeName = "";
            if (ClearBeforeTypeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                typeFilter = int.Parse(selectedItem.Tag.ToString());
                typeName = selectedItem.Content.ToString();
            }

            string confirmMsg;
            if (days == 0)
            {
                if (typeFilter == -1 || typeFilter == null)
                {
                    confirmMsg = Loc.Get("Settings.History.ClearAllTypeConfirm", "确定要删除所有未分组历史记录吗？\n\n已分组的记录将保留。");
                }
                else
                {
                    confirmMsg = string.Format(
                        Loc.Get("Settings.History.ClearAllTypeConfirmWithType", "确定要删除所有 {0} 类型的未分组历史记录吗？\n\n已分组的记录将保留。"),
                        typeName);
                }
            }
            else
            {
                if (typeFilter == -1 || typeFilter == null)
                {
                    confirmMsg = string.Format(
                        Loc.Get("Settings.History.ClearBeforeDaysConfirm", "确定要删除 {0} 天之前的未分组历史记录吗？\n\n已分组的记录将保留。"),
                        days);
                }
                else
                {
                    confirmMsg = string.Format(
                        Loc.Get("Settings.History.ClearBeforeDaysConfirmWithType", "确定要删除 {0} 天之前的 {1} 类型未分组历史记录吗？\n\n已分组的记录将保留。"),
                        days, typeName);
                }
            }

            var result = MessageBox.Show(
                confirmMsg,
                Loc.Get("Message.Confirm", "确认"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                App.DatabaseService.DeleteOldItems(cutoffDate, typeFilter);
                App.GetMainWindow()?.ViewModel?.LoadItems();
                MessageBox.Show(Loc.Get("Message.ClearSuccess", "删除成功"), Loc.Get("Message.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(Loc.Get("Settings.Reset.Confirm", "确定要重置所有设置吗？这将恢复到默认配置。"), Loc.Get("Settings.Reset.Title", "确认重置"), 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _settingsService.Settings.Hotkey = "Ctrl+Shift+V";
                _settingsService.Settings.CaptureImages = true;
                _settingsService.Settings.CaptureFiles = true;
                _settingsService.Settings.RemoveDuplicates = true;
                _settingsService.Settings.EnableAutoCleanup = false;
                _settingsService.Settings.RetentionDays = 30;
                _settingsService.Settings.MaxHistoryItems = 200;
                _settingsService.Settings.MonitorEnabled = true;
                _settingsService.Settings.Theme = "Auto";
                _settingsService.Settings.SelectedSearchEngineId = "bing";
                _settingsService.Settings.CustomSearchEngineUrl = "";
                _settingsService.Settings.FontSize = 14;
                _settingsService.Settings.ShowHotkeyTipOnStartup = true;
                _settingsService.Settings.QuickPasteShortcuts = QuickPasteShortcut.CreateDefaults();
                _settingsService.Settings.QuickCommands = QuickCommand.CreateDefaults();
                _settingsService.Settings.GlobalHotkeys = GlobalHotkey.CreateDefaults();

                if (StartWithWindowsCheckBox != null)
                {
                    StartWithWindowsCheckBox.IsChecked = false;
                }

                DataContext = null;
                DataContext = _settingsService.Settings;

                UpdateCustomSearchEngineGridVisibility();
                // 重建条目
                QuickPasteEntriesPanel.Children.Clear();
                LoadQuickPasteEntries();
                QuickCommandEntriesPanel.Children.Clear();
                LoadQuickCommandEntries();
                GlobalHotkeyEntriesPanel.Children.Clear();
                LoadGlobalHotkeyEntries();
                ValidateAllEntries();

                MessageBox.Show(Loc.Get("Settings.Reset.Success", "设置已重置为默认值。点击\"应用\"或\"确定\"保存更改。"), Loc.Get("Settings.Reset.SuccessTitle", "重置成功"), 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #region 便捷粘贴快捷键条目

        /// <summary>便捷粘贴条目控件的命名约定，便于在代码中按名称查找。</summary>
        private const string EntryTypeComboBox = "TypeComboBox";
        private const string EntryCtrlCheckBox = "CtrlCheckBox";
        private const string EntryShiftCheckBox = "ShiftCheckBox";
        private const string EntryAltCheckBox = "AltCheckBox";
        private const string EntryMouseComboBox = "MouseComboBox";
        private const string EntryActionComboBox = "ActionComboBox";
        private const string EntryEnabledCheckBox = "EnabledCheckBox";

        /// <summary>从已保存设置加载并构建所有便捷粘贴条目。</summary>
        private void LoadQuickPasteEntries()
        {
            QuickPasteEntriesPanel.Children.Clear();
            var list = _settingsService.Settings.QuickPasteShortcuts;
            if (list != null)
            {
                foreach (var sc in list)
                    QuickPasteEntriesPanel.Children.Add(BuildQuickPasteEntry(sc.Clone()));
            }
        }

        /// <summary>➕按钮：新增一条默认值条目。</summary>
        private void AddQuickPasteEntry_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new QuickPasteShortcut
            {
                ClipboardType = -1,
                Ctrl = false,
                Shift = false,
                Alt = false,
                MouseButton = QuickPasteMouseButton.Middle,
                Action = QuickPasteAction.PasteRemoveNewlines
            };
            QuickPasteEntriesPanel.Children.Add(BuildQuickPasteEntry(defaults));
        }

        /// <summary>构建单条便捷粘贴快捷键条目 UI。</summary>
        private Border BuildQuickPasteEntry(QuickPasteShortcut saved)
        {
            // 卡片样式边框，可通过确认按钮改绿色
            var entryBorder = new Border
            {
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Background = (Brush)FindResource("InputBackground"),
                Tag = saved.Clone() // 已保存快照，供重置使用
            };

            var root = new StackPanel { Margin = new Thickness(0) };
            entryBorder.Child = root;

            // ===== 第一行：类型 + 修饰键 + 鼠标按键 + 操作 =====
            var row1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

            var typeCombo = BuildClipboardTypeCombo(EntryTypeComboBox, saved.ClipboardType);
            row1.Children.Add(typeCombo);

            row1.Children.Add(new TextBlock
            {
                Text = "：",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                Foreground = (Brush)FindResource("TextForeground")
            });

            var ctrlCb = BuildModifierCheckBox(EntryCtrlCheckBox, "Ctrl", saved.Ctrl);
            row1.Children.Add(ctrlCb);

            var shiftCb = BuildModifierCheckBox(EntryShiftCheckBox, "Shift", saved.Shift);
            row1.Children.Add(shiftCb);

            var altCb = BuildModifierCheckBox(EntryAltCheckBox, "Alt", saved.Alt);
            row1.Children.Add(altCb);

            var mouseCombo = new ComboBox
            {
                Name = EntryMouseComboBox,
                Width = 110,
                Style = (Style)FindResource("ModernComboBoxStyle"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            mouseCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.MouseLeft", "鼠标左键"), QuickPasteMouseButton.Left.ToString()));
            mouseCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.MouseMiddle", "鼠标中键"), QuickPasteMouseButton.Middle.ToString()));
            mouseCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.WheelUp", "鼠标滚轮上"), QuickPasteMouseButton.WheelUp.ToString()));
            mouseCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.WheelDown", "鼠标滚轮下"), QuickPasteMouseButton.WheelDown.ToString()));
            SelectComboByTag(mouseCombo, saved.MouseButton.ToString());
            row1.Children.Add(mouseCombo);

            row1.Children.Add(new TextBlock
            {
                Text = "→",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                Foreground = (Brush)FindResource("TextForeground")
            });

            var actionCombo = BuildActionComboBox(EntryActionComboBox, saved.Action, saved.QuickCommandName,
                saved.ClipboardType);
            // 下拉展开时按当前条目类型过滤快捷命令（与「快捷命令」tab 改动同步）
            actionCombo.DropDownOpened += (s, e) =>
            {
                var selectedTag = (actionCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                int filterType = -1;
                for (var dp = (System.Windows.DependencyObject)actionCombo; dp != null; dp = System.Windows.Media.VisualTreeHelper.GetParent(dp))
                {
                    if (dp is Border b && b.Tag is QuickPasteShortcut)
                    {
                        filterType = ReadTypeComboFromEntryBorder(b, EntryTypeComboBox);
                        break;
                    }
                }
                RefreshActionComboQuickCommands(actionCombo, filterType);
                SelectComboByTag(actionCombo, selectedTag);
            };
            row1.Children.Add(actionCombo);

            root.Children.Add(row1);

            // 任一控件被修改后，移除绿色"已确认"边框，提示用户需重新确认
            typeCombo.SelectionChanged += (s, e) => ResetEntryBorder(entryBorder);
            mouseCombo.SelectionChanged += (s, e) => ResetEntryBorder(entryBorder);
            actionCombo.SelectionChanged += (s, e) => ResetEntryBorder(entryBorder);
            ctrlCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            shiftCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            altCb.Click += (s, e) => ResetEntryBorder(entryBorder);

            // ===== 第三行：启用 / 重置 / 删除 / 确定（靠右） =====
            var row3 = BuildEntryButtons(
                EntryEnabledCheckBox, saved.Enabled,
                (s, e) => OnQuickPasteConfirm(entryBorder),
                (s, e) => OnQuickPasteReset(entryBorder),
                System.Windows.Threading.DispatcherPriority.Background,
                () => QuickPasteEntriesPanel.Children.Remove(entryBorder));
            root.Children.Add(row3);

            return entryBorder;
        }

        private ComboBoxItem MakeComboItem(string content, string tag)
        {
            return new ComboBoxItem { Content = content, Tag = tag };
        }

        // ===== 公共构建辅助（主界面快捷键 / 全局快捷键复用） =====

        /// <summary>构建单个修饰键 CheckBox。</summary>
        private CheckBox BuildModifierCheckBox(string name, string content, bool isChecked)
        {
            return new CheckBox
            {
                Name = name,
                Content = content,
                IsChecked = isChecked,
                Style = (Style)FindResource("ModernCheckBoxStyle"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>向 ActionComboBox 填充 10 个内建静态操作项（不含动态快捷命令分组）。</summary>
        private void FillStaticActionItems(ComboBox actionCombo)
        {
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.CharPicker", "拆分选字"), QuickPasteAction.Split.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.OpenInBrowser", "在浏览器打开"), QuickPasteAction.OpenInBrowser.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.GenerateQRCode", "生成二维码"), QuickPasteAction.GenerateQRCode.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.Edit", "编辑"), QuickPasteAction.Edit.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.Copy", "复制"), QuickPasteAction.Copy.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.Delete", "删除"), QuickPasteAction.Delete.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.PasteAsText", "以纯文本粘贴"), QuickPasteAction.PasteAsPlainText.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.PasteRemoveNewlines", "移除换行符以纯文本粘贴"), QuickPasteAction.PasteRemoveNewlines.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.PasteAndDelete", "粘贴后删除"), QuickPasteAction.PasteAndDelete.ToString()));
            actionCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.ContextMenu.Group", "分组"), QuickPasteAction.Group.ToString()));
        }

        /// <summary>构建 ActionComboBox 并完成初始化选中。filterType=-1 表示不按类型过滤快捷命令。
        /// 注意：DropDownOpened 时的动态刷新由调用方自行挂载（不同条目类型过滤策略不同）。</summary>
        private ComboBox BuildActionComboBox(string elementName, QuickPasteAction savedAction,
            string savedQcName, int initialFilterType)
        {
            var actionCombo = new ComboBox
            {
                Name = elementName,
                Width = 200,
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            FillStaticActionItems(actionCombo);
            // 动态项：构建时先插一次 QC 分组，便于初始化选中已保存的 QC 名称
            RefreshActionComboQuickCommands(actionCombo, initialFilterType);
            // 初始化选中值
            string initTag;
            if (savedAction == QuickPasteAction.CustomRegex && !string.IsNullOrEmpty(savedQcName))
                initTag = "QC:" + savedQcName;
            else
                initTag = savedAction.ToString();
            SelectComboByTag(actionCombo, initTag);
            return actionCombo;
        }

        /// <summary>从指定元素向上查找 Tag 为指定类型字符串的 Border。</summary>
        private static Border FindAncestorBorder(System.Windows.DependencyObject start, string tagType)
        {
            for (var dp = start; dp != null; dp = System.Windows.Media.VisualTreeHelper.GetParent(dp))
            {
                if (dp is Border b && b.Tag != null && b.Tag.GetType().Name == tagType)
                    return b;
            }
            return null;
        }

        /// <summary>构建"重置 / 删除 / 确定"按钮行（靠右排列）。</summary>
        private DockPanel BuildEntryButtons(string enabledCheckBoxName, bool enabledValue,
            EventHandler confirmHandler, EventHandler resetHandler,
            System.Windows.Threading.DispatcherPriority removePriority, System.Action removeAction)
        {
            var row = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 0) };

            var confirmBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Confirm", "确定"),
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(6, 0, 0, 0)
            };
            confirmBtn.Click += (s, e) => confirmHandler(s, e);
            DockPanel.SetDock(confirmBtn, Dock.Right);
            row.Children.Add(confirmBtn);

            var deleteBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Delete", "删除"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(6, 0, 0, 0)
            };
            deleteBtn.Click += (s, e) => Dispatcher.BeginInvoke(new Action(removeAction), removePriority);
            DockPanel.SetDock(deleteBtn, Dock.Right);
            row.Children.Add(deleteBtn);

            var resetBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Reset", "重置"),
                Style = (Style)FindResource("SecondaryButtonStyle")
            };
            resetBtn.Click += (s, e) => resetHandler(s, e);
            DockPanel.SetDock(resetBtn, Dock.Right);
            row.Children.Add(resetBtn);

            // 启用 CheckBox：位于重置按钮左边（Dock.Right 逆序排列，最后添加 = 最左）
            var enabledCb = new CheckBox
            {
                Name = enabledCheckBoxName,
                Content = Loc.Get("Settings.Entry.Enabled", "启用"),
                IsChecked = enabledValue,
                Style = (Style)FindResource("ModernCheckBoxStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            DockPanel.SetDock(enabledCb, Dock.Right);
            row.Children.Add(enabledCb);

            return row;
        }

        /// <summary>构建公共的 ClipboardType 选择下拉框（所有类型/文本/图片/文件/富文本）。</summary>
        private ComboBox BuildClipboardTypeCombo(string elementName, int selectedType)
        {
            var typeCombo = new ComboBox
            {
                Name = elementName,
                Width = 110,
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            typeCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.TypeAll", "所有类型"), "-1"));
            typeCombo.Items.Add(MakeComboItem(Loc.Get("Settings.Hotkey.QuickPaste.TypeTextAndRichText", "文本和富文本"), "-2"));
            typeCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.Filter.Text", "文本"), "0"));
            typeCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.Filter.Image", "图片"), "1"));
            typeCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.Filter.File", "文件"), "2"));
            typeCombo.Items.Add(MakeComboItem(Loc.Get("MainWindow.Filter.RichText", "富文本"), "3"));
            SelectComboByTag(typeCombo, selectedType.ToString());
            return typeCombo;
        }

        /// <summary>读取 ClipboardType ComboBox 当前选中值，默认 -1。</summary>
        private int ReadClipboardTypeCombo(ComboBox typeCombo)
        {
            if (typeCombo == null) return -1;
            var tag = (typeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            if (int.TryParse(tag, out var v)) return v;
            return -1;
        }

        /// <summary>
        /// 动态刷新 ActionComboBox 末尾的「快捷命令」分组：
        ///  1. 先移除之前插入的所有动态项（用 Tag 约定 "__QC_SEP__" / "__QC_HEADER__" / "QC:xxx" 识别）
        ///  2. 再追加 Separator → 分组标题 → 每个快捷命令一项
        ///  如果没有任何快捷命令 → 只追加 Separator + 标题 + "（暂无，请在「快捷命令」tab 添加）"
        /// </summary>
        /// <summary>
        /// 从 QuickPasteEntry Border 内部查找指定名称的 ComboBox，返回其当前 ClipboardType 值，未找到返回 -1。
        /// </summary>
        private int ReadTypeComboFromEntryBorder(Border entryBorder, string comboName)
        {
            if (entryBorder == null) return -1;
            var stack = new Stack<System.Windows.DependencyObject>();
            stack.Push(entryBorder);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(p);
                for (int i = 0; i < count; i++)
                {
                    var c = System.Windows.Media.VisualTreeHelper.GetChild(p, i);
                    if (c is ComboBox cb && cb.Name == comboName)
                        return ReadClipboardTypeCombo(cb);
                    stack.Push(c);
                }
            }
            return -1;
        }

        private void RefreshActionComboQuickCommands(ComboBox combo, int entryTypeFilter = -1)
        {
            if (combo == null) return;
            // 1. 清理旧的 QC 动态项（从后往前删）
            for (int i = combo.Items.Count - 1; i >= 0; i--)
            {
                var it = combo.Items[i];
                if (it is FrameworkElement fe && fe.Tag is string sTag)
                {
                    if (sTag == "__QC_SEP__" || sTag == "__QC_HEADER__" || sTag.StartsWith("QC:"))
                        combo.Items.RemoveAt(i);
                }
            }

            // 2. 追加 Separator + 分组标题 + QC 列表项
            var sep = new Separator { Tag = "__QC_SEP__" };
            combo.Items.Add(sep);
            var header = new ComboBoxItem
            {
                Tag = "__QC_HEADER__",
                IsHitTestVisible = false,
                Focusable = false,
                FontSize = 11,
                Foreground = (Brush)FindResource("SecondaryTextForeground"),
                Margin = new Thickness(6, 4, 6, 2),
                Content = "—— " + Loc.Get("Settings.QuickCommand.Title", "快捷命令") + " ——"
            };
            combo.Items.Add(header);

            var list = _settingsService.Settings.QuickCommands;
            bool any = false;
            if (list != null)
            {
                foreach (var qc in list)
                {
                    var name = (qc.Name ?? "").Trim();
                    if (name.Length == 0) continue;
                    if (!qc.Enabled) continue;
                    if (entryTypeFilter != -1 && !Models.QuickCommand.TypesIntersect(qc.ClipboardType, entryTypeFilter)) continue;
                    combo.Items.Add(MakeComboItem(name, "QC:" + name));
                    any = true;
                }
            }
            if (!any)
            {
                var empty = new ComboBoxItem
                {
                    Tag = "QC:__EMPTY__",
                    IsEnabled = false,
                    Foreground = (Brush)FindResource("SecondaryTextForeground"),
                    Content = "(" + Loc.Get("Settings.Hotkey.QuickPaste.QcEmpty", "暂无，请在「快捷命令」tab 添加") + ")"
                };
                combo.Items.Add(empty);
            }
        }

        private void SelectComboByTag(ComboBox combo, string tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Tag as string == tag)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        /// <summary>恢复条目边框为默认样式（移除绿色"已确认"标记）。</summary>
        private void ResetEntryBorder(Border entry)
        {
            entry.BorderBrush = (Brush)FindResource("InputBorderBrush");
            entry.BorderThickness = new Thickness(1);
        }

        /// <summary>设置条目边框为绿色"已确认"样式。</summary>
        private void SetEntryConfirmedBorder(Border entry)
        {
            entry.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            entry.BorderThickness = new Thickness(2);
        }

        /// <summary>设置界面打开/重置后批量校验所有条目并设置边框颜色（不弹框）。</summary>
        private void ValidateAllEntries()
        {
            foreach (var child in QuickPasteEntriesPanel.Children)
                if (child is Border entry) ValidateQuickPasteEntry(entry, showMessage: false);
            foreach (var child in GlobalHotkeyEntriesPanel.Children)
                if (child is Border entry) ValidateGlobalHotkeyEntry(entry, showMessage: false);
            foreach (var child in QuickCommandEntriesPanel.Children)
                if (child is Border entry) ValidateQuickCommandEntry(entry, showMessage: false);
        }

        /// <summary>确定按钮：检测快捷键可用性，可用则绿框，不可用则弹框提醒。</summary>
        private void OnQuickPasteConfirm(Border entry)
        {
            ValidateQuickPasteEntry(entry, showMessage: true);
        }

        /// <summary>校验便捷粘贴条目：可用则绿框。showMessage=true 时弹框提醒冲突。</summary>
        private bool ValidateQuickPasteEntry(Border entry, bool showMessage)
        {
            var current = ReadQuickPasteEntry(entry);
            if (IsShortcutAvailable(current, entry, out string conflictDesc))
            {
                SetEntryConfirmedBorder(entry);
                return true;
            }
            ResetEntryBorder(entry);
            if (showMessage)
            {
                MessageBox.Show(
                    string.Format(Loc.Get("Settings.Hotkey.QuickPaste.ConflictMessage", "快捷键冲突：{0}"), conflictDesc),
                    Loc.Get("Settings.Hotkey.QuickPaste.ConflictTitle", "快捷键不可用"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        /// <summary>重置按钮：恢复该条目到已保存快照。</summary>
        private void OnQuickPasteReset(Border entry)
        {
            var saved = entry.Tag as QuickPasteShortcut;
            if (saved == null) return;
            var rebuilt = BuildQuickPasteEntry(saved.Clone());
            // 延迟到当前 Click 事件处理完毕后再修改可视化树，避免移除触发中的按钮导致崩溃。
            // UIElementCollection 索引器 setter 不支持直接替换（抛 ArgumentException），须先 RemoveAt 再 Insert。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var idx = QuickPasteEntriesPanel.Children.IndexOf(entry);
                if (idx >= 0)
                {
                    QuickPasteEntriesPanel.Children.RemoveAt(idx);
                    QuickPasteEntriesPanel.Children.Insert(idx, rebuilt);
                    // 重置后重新校验，恢复绿框（若快照值仍合法）
                    ValidateQuickPasteEntry(rebuilt, showMessage: false);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>读取单条条目当前控件值为 QuickPasteShortcut。</summary>
        private QuickPasteShortcut ReadQuickPasteEntry(Border entry)
        {
            var sc = new QuickPasteShortcut();
            if (entry.Child is StackPanel root)
            {
                if (root.Children[0] is WrapPanel row1)
                {
                    if (row1.Children[0] is ComboBox typeCombo)
                        sc.ClipboardType = int.Parse((typeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "-1");
                    // 遍历查找三个 CheckBox（按名称识别）
                    foreach (var child in row1.Children)
                    {
                        if (child is CheckBox cb)
                        {
                            if (cb.Name == EntryCtrlCheckBox) sc.Ctrl = cb.IsChecked == true;
                            else if (cb.Name == EntryShiftCheckBox) sc.Shift = cb.IsChecked == true;
                            else if (cb.Name == EntryAltCheckBox) sc.Alt = cb.IsChecked == true;
                        }
                    }
                    foreach (var child in row1.Children)
                    {
                        if (child is ComboBox combo)
                        {
                            if (combo.Name == EntryMouseComboBox)
                                sc.MouseButton = ParseMouseCombo(combo);
                            else if (combo.Name == EntryActionComboBox)
                            {
                                var actTag = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
                                if (actTag != null && actTag.StartsWith("QC:"))
                                {
                                    sc.Action = QuickPasteAction.CustomRegex;
                                    sc.QuickCommandName = actTag.Substring(3);
                                }
                                else
                                {
                                    sc.Action = ParseActionCombo(combo);
                                    sc.QuickCommandName = "";
                                }
                            }
                        }
                    }
                }
                // 读取启用 CheckBox
                var enabledCb = FindQcChildByName<CheckBox>(entry, EntryEnabledCheckBox);
                if (enabledCb != null) sc.Enabled = enabledCb.IsChecked == true;
            }
            return sc;
        }

        private QuickPasteMouseButton ParseMouseCombo(ComboBox combo)
        {
            var tag = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
            return Enum.TryParse<QuickPasteMouseButton>(tag, out var m) ? m : QuickPasteMouseButton.Middle;
        }

        private QuickPasteAction ParseActionCombo(ComboBox combo)
        {
            var tag = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
            // 选择 QC 分组项下的快捷命令 → 视为 CustomRegex（真正的命令名保存在 QuickCommandName 字段）
            if (tag != null && tag.StartsWith("QC:"))
                return QuickPasteAction.CustomRegex;
            return Enum.TryParse<QuickPasteAction>(tag, out var a) ? a : QuickPasteAction.PasteRemoveNewlines;
        }

        /// <summary>冲突检测：当前条目与其他条目是否冲突。</summary>
        private bool IsShortcutAvailable(QuickPasteShortcut current, UIElement exceptSelf, out string conflictDesc)
        {
            conflictDesc = "";
            foreach (var child in QuickPasteEntriesPanel.Children)
            {
                if (child == exceptSelf || !(child is Border other)) continue;
                var otherSc = ReadQuickPasteEntry(other);
                if (ShortcutConflicts(current, otherSc))
                {
                    conflictDesc = DescribeShortcut(current);
                    return false;
                }
            }
            return true;
        }

        /// <summary>两条快捷键是否冲突：鼠标按键相同 && 修饰键组合相同 && (类型相同 || 其中一个为所有类型)。</summary>
        private bool ShortcutConflicts(QuickPasteShortcut a, QuickPasteShortcut b)
        {
            if (a.MouseButton != b.MouseButton) return false;
            if (a.Ctrl != b.Ctrl || a.Shift != b.Shift || a.Alt != b.Alt) return false;
            // 类型：-1 = 所有类型。所有类型与任何具体类型都冲突
            if (a.ClipboardType == -1 || b.ClipboardType == -1) return true;
            return a.ClipboardType == b.ClipboardType;
        }

        private string DescribeShortcut(QuickPasteShortcut sc)
        {
            var mod = "";
            if (sc.Ctrl) mod += "Ctrl+";
            if (sc.Shift) mod += "Shift+";
            if (sc.Alt) mod += "Alt+";
            var type = sc.ClipboardType == -1 ? Loc.Get("Settings.Hotkey.QuickPaste.TypeAll", "所有类型")
                : ((ClipboardType)sc.ClipboardType).ToString();
            return $"{type} + {mod}{sc.MouseButton}";
        }

        /// <summary>收集所有条目当前值到设置列表。</summary>
        private void CollectQuickPasteEntries()
        {
            var list = new List<QuickPasteShortcut>();
            foreach (var child in QuickPasteEntriesPanel.Children)
            {
                if (child is Border entry)
                    list.Add(ReadQuickPasteEntry(entry));
            }
            _settingsService.Settings.QuickPasteShortcuts = list;
        }

        /// <summary>同步每条条目的「已保存快照」为当前值（应用后调用）。</summary>
        private void SyncQuickPasteEntrySnapshots()
        {
            foreach (var child in QuickPasteEntriesPanel.Children)
            {
                if (child is Border entry)
                    entry.Tag = ReadQuickPasteEntry(entry).Clone();
            }
        }

        #endregion

        #region 全局快捷键条目

        /// <summary>全局快捷键条目控件的命名约定。</summary>
        private const string GhItemIndexComboBox = "GhItemIndexComboBox";
        private const string GhCtrlCheckBox = "GhCtrlCheckBox";
        private const string GhShiftCheckBox = "GhShiftCheckBox";
        private const string GhAltCheckBox = "GhAltCheckBox";
        private const string GhWinCheckBox = "GhWinCheckBox";
        private const string GhKeyTextBox = "GhKeyTextBox";
        private const string GhActionComboBox = "GhActionComboBox";
        private const string GhEnabledCheckBox = "GhEnabledCheckBox";

        /// <summary>从已保存设置加载并构建所有全局快捷键条目。</summary>
        private void LoadGlobalHotkeyEntries()
        {
            GlobalHotkeyEntriesPanel.Children.Clear();
            var list = _settingsService.Settings.GlobalHotkeys;
            if (list != null)
            {
                foreach (var gh in list)
                    GlobalHotkeyEntriesPanel.Children.Add(BuildGlobalHotkeyEntry(gh.Clone()));
            }
        }

        /// <summary>➕ 全局快捷键按钮：新增一条默认条目（条目容器在按钮上方，Add 即可）。</summary>
        private void AddGlobalHotkeyEntry_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new GlobalHotkey
            {
                ItemIndex = 1,
                Ctrl = true,
                Shift = false,
                Alt = false,
                Win = false,
                Key = "",
                Action = QuickPasteAction.PasteAsPlainText
            };
            GlobalHotkeyEntriesPanel.Children.Add(BuildGlobalHotkeyEntry(defaults));
        }

        /// <summary>构建单条全局快捷键条目 UI。</summary>
        private Border BuildGlobalHotkeyEntry(GlobalHotkey saved)
        {
            if (saved == null) saved = new GlobalHotkey();

            var entryBorder = new Border
            {
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Background = (Brush)FindResource("InputBackground"),
                Tag = saved.Clone()
            };

            var root = new StackPanel { Margin = new Thickness(0) };
            entryBorder.Child = root;

            // ===== 第一行：第N项 + "：" + Ctrl/Shift/Alt/Win + 按键 + → + 操作 =====
            var row1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };

            var indexCombo = new ComboBox
            {
                Name = GhItemIndexComboBox,
                Width = 90,
                Style = (Style)FindResource("ModernComboBoxStyle"),
                Margin = new Thickness(0, 0, 4, 0)
            };
            for (int i = 1; i <= 10; i++)
                indexCombo.Items.Add(MakeComboItem(string.Format(Loc.Get("Settings.Hotkey.Global.ItemN", "第{0}项"), i), i.ToString()));
            SelectComboByTag(indexCombo, saved.ItemIndex.ToString());
            row1.Children.Add(indexCombo);

            row1.Children.Add(new TextBlock
            {
                Text = "：",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                Foreground = (Brush)FindResource("TextForeground")
            });

            var ctrlCb = BuildModifierCheckBox(GhCtrlCheckBox, "Ctrl", saved.Ctrl);
            row1.Children.Add(ctrlCb);

            var shiftCb = BuildModifierCheckBox(GhShiftCheckBox, "Shift", saved.Shift);
            row1.Children.Add(shiftCb);

            var altCb = BuildModifierCheckBox(GhAltCheckBox, "Alt", saved.Alt);
            row1.Children.Add(altCb);

            var winCb = BuildModifierCheckBox(GhWinCheckBox, "Win", saved.Win);
            row1.Children.Add(winCb);

            // 按键输入框：捕获单个按键（修饰键已由 CheckBox 表示，这里只记录普通按键）
            var keyBox = new TextBox
            {
                Name = GhKeyTextBox,
                Text = saved.Key ?? "",
                Width = 90,
                IsReadOnly = true,
                Style = (Style)FindResource("InputTextBoxStyle"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = Loc.Get("Settings.Hotkey.Global.KeyTip", "点击后按下键盘按键")
            };
            // 占位提示
            var keyPlaceholder = new TextBlock
            {
                Text = Loc.Get("Settings.Hotkey.Global.KeyPlaceholder", "按键"),
                Foreground = (Brush)FindResource("SecondaryTextForeground"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var keyGrid = new Grid { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            keyBox.Margin = new Thickness(0);
            keyGrid.Children.Add(keyBox);
            keyGrid.Children.Add(keyPlaceholder);
            keyPlaceholder.Visibility = string.IsNullOrEmpty(saved.Key) ? Visibility.Visible : Visibility.Collapsed;
            keyBox.TextChanged += (s, e) =>
                keyPlaceholder.Visibility = string.IsNullOrEmpty(keyBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            // 捕获按键：聚焦后按下任意普通按键即记录其名称；Esc 清空
            keyBox.GotFocus += (s, e) => keyBox.Text = "";
            keyBox.PreviewKeyDown += (s, e) =>
            {
                e.Handled = true;
                if (e.Key == Key.System) return; // 忽略 Alt 系列系统键
                if (IsModifierKey(e.Key)) return; // 修饰键不作为主键
                if (e.Key == Key.Escape)
                {
                    keyBox.Text = "";
                    return;
                }
                keyBox.Text = GetKeyDisplayName(e.Key);
                // 记录后自动失焦，避免误触
                keyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            };
            row1.Children.Add(keyGrid);

            row1.Children.Add(new TextBlock
            {
                Text = "→",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                Foreground = (Brush)FindResource("TextForeground")
            });

            var actionCombo = BuildActionComboBox(GhActionComboBox, saved.Action, saved.QuickCommandName, -1);
            // 全局快捷键不按类型过滤快捷命令（作用于固定序号条目，类型不定）
            actionCombo.DropDownOpened += (s, e) =>
            {
                var selectedTag = (actionCombo.SelectedItem as ComboBoxItem)?.Tag as string;
                RefreshActionComboQuickCommands(actionCombo, -1);
                SelectComboByTag(actionCombo, selectedTag);
            };
            row1.Children.Add(actionCombo);

            root.Children.Add(row1);

            // 任一控件被修改后，移除绿色"已确认"边框
            indexCombo.SelectionChanged += (s, e) => ResetEntryBorder(entryBorder);
            actionCombo.SelectionChanged += (s, e) => ResetEntryBorder(entryBorder);
            ctrlCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            shiftCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            altCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            winCb.Click += (s, e) => ResetEntryBorder(entryBorder);
            keyBox.TextChanged += (s, e) => ResetEntryBorder(entryBorder);

            // ===== 第二行：启用 / 重置 / 删除 / 确定 =====
            var row3 = BuildEntryButtons(
                GhEnabledCheckBox, saved.Enabled,
                (s, e) => OnGlobalHotkeyConfirm(entryBorder),
                (s, e) => OnGlobalHotkeyReset(entryBorder),
                System.Windows.Threading.DispatcherPriority.Background,
                () =>
                {
                    // 删除时保留 ➕ 按钮（它是面板最后一个子元素）
                    var idx = GlobalHotkeyEntriesPanel.Children.IndexOf(entryBorder);
                    if (idx >= 0) GlobalHotkeyEntriesPanel.Children.RemoveAt(idx);
                });
            root.Children.Add(row3);

            return entryBorder;
        }

        /// <summary>确定按钮：校验快捷键可用性，可用则绿框。</summary>
        private void OnGlobalHotkeyConfirm(Border entry)
        {
            ValidateGlobalHotkeyEntry(entry, showMessage: true);
        }

        /// <summary>校验全局快捷键条目：可用则绿框。showMessage=true 时弹框提醒无效/冲突。</summary>
        private bool ValidateGlobalHotkeyEntry(Border entry, bool showMessage)
        {
            var current = ReadGlobalHotkeyEntry(entry);
            var hotkeyStr = current.ToHotkeyString();
            if (string.IsNullOrEmpty(hotkeyStr) || !HotkeyService.ValidateHotkey(hotkeyStr))
            {
                ResetEntryBorder(entry);
                if (showMessage)
                {
                    MessageBox.Show(
                        Loc.Get("Settings.Hotkey.Global.Invalid", "快捷键无效：需至少一个修饰键+按键，或单独 F1-F12 功能键。"),
                        Loc.Get("Settings.Hotkey.QuickPaste.ConflictTitle", "快捷键不可用"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }
            // 冲突检测：与其它全局快捷键条目 + 显示/隐藏主界面热键
            if (!IsGlobalHotkeyAvailable(current, entry, out string conflictDesc))
            {
                ResetEntryBorder(entry);
                if (showMessage)
                {
                    MessageBox.Show(
                        string.Format(Loc.Get("Settings.Hotkey.QuickPaste.ConflictMessage", "快捷键冲突：{0}"), conflictDesc),
                        Loc.Get("Settings.Hotkey.QuickPaste.ConflictTitle", "快捷键不可用"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }
            SetEntryConfirmedBorder(entry);
            return true;
        }

        /// <summary>重置按钮：恢复该条目到已保存快照。</summary>
        private void OnGlobalHotkeyReset(Border entry)
        {
            var saved = entry.Tag as GlobalHotkey;
            if (saved == null) return;
            var rebuilt = BuildGlobalHotkeyEntry(saved.Clone());
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var idx = GlobalHotkeyEntriesPanel.Children.IndexOf(entry);
                if (idx >= 0)
                {
                    GlobalHotkeyEntriesPanel.Children.RemoveAt(idx);
                    GlobalHotkeyEntriesPanel.Children.Insert(idx, rebuilt);
                    // 重置后重新校验，恢复绿框（若快照值仍合法）
                    ValidateGlobalHotkeyEntry(rebuilt, showMessage: false);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>读取单条全局快捷键条目当前控件值。</summary>
        private GlobalHotkey ReadGlobalHotkeyEntry(Border entry)
        {
            var gh = new GlobalHotkey();
            if (entry.Child is StackPanel root && root.Children[0] is WrapPanel row1)
            {
                foreach (var child in row1.Children)
                {
                    if (child is ComboBox combo)
                    {
                        if (combo.Name == GhItemIndexComboBox)
                        {
                            var tag = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
                            if (int.TryParse(tag, out var v)) gh.ItemIndex = v;
                        }
                        else if (combo.Name == GhActionComboBox)
                        {
                            var actTag = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
                            if (actTag != null && actTag.StartsWith("QC:"))
                            {
                                gh.Action = QuickPasteAction.CustomRegex;
                                gh.QuickCommandName = actTag.Substring(3);
                            }
                            else
                            {
                                gh.Action = ParseActionCombo(combo);
                                gh.QuickCommandName = "";
                            }
                        }
                    }
                    else if (child is CheckBox cb)
                    {
                        if (cb.Name == GhCtrlCheckBox) gh.Ctrl = cb.IsChecked == true;
                        else if (cb.Name == GhShiftCheckBox) gh.Shift = cb.IsChecked == true;
                        else if (cb.Name == GhAltCheckBox) gh.Alt = cb.IsChecked == true;
                        else if (cb.Name == GhWinCheckBox) gh.Win = cb.IsChecked == true;
                    }
                    else if (child is Grid g)
                    {
                        // 按键输入框在 Grid 内
                        foreach (var c in g.Children)
                        {
                            if (c is TextBox tb && tb.Name == GhKeyTextBox)
                                gh.Key = tb.Text ?? "";
                        }
                    }
                }
            }
            // 读取启用 CheckBox
            var enabledCb = FindQcChildByName<CheckBox>(entry, GhEnabledCheckBox);
            if (enabledCb != null) gh.Enabled = enabledCb.IsChecked == true;
            return gh;
        }

        /// <summary>
        /// 冲突检测：与其它全局快捷键条目 + 显示/隐藏主界面热键是否冲突。
        /// 三重检测：① 字符串比对显示/隐藏主界面热键 ② 字符串比对其它全局快捷键条目
        /// ③ 使用与显示/隐藏主界面相同的 RegisterHotKey API 验证是否被其它应用占用。
        /// </summary>
        private bool IsGlobalHotkeyAvailable(GlobalHotkey current, UIElement exceptSelf, out string conflictDesc)
        {
            conflictDesc = "";
            var rawStr = current.ToHotkeyString();
            var curStr = HotkeyService.NormalizeHotkey(rawStr);
            // 1. 与显示/隐藏主界面热键冲突
            var showHide = HotkeyService.NormalizeHotkey(_settingsService.Settings.Hotkey);
            if (!string.IsNullOrEmpty(showHide) && string.Equals(curStr, showHide, StringComparison.OrdinalIgnoreCase))
            {
                conflictDesc = curStr + " (" + Loc.Get("Settings.Hotkey.Global.ConflictShowHide", "与显示/隐藏主界面热键冲突") + ")";
                return false;
            }
            // 2. 与其它全局快捷键条目冲突
            foreach (var child in GlobalHotkeyEntriesPanel.Children)
            {
                if (child == exceptSelf || !(child is Border other)) continue;
                var otherGh = ReadGlobalHotkeyEntry(other);
                var otherStr = HotkeyService.NormalizeHotkey(otherGh.ToHotkeyString());
                if (string.Equals(curStr, otherStr, StringComparison.OrdinalIgnoreCase))
                {
                    conflictDesc = curStr;
                    return false;
                }
            }
            // 3. 若与上次 Apply/启动时已注册的本应用热键相同，则跳过 API 注册测试——
            //    避免与自身已注册热键产生误报（Apply 时会统一注销并重新注册全部热键）。
            if (IsOwnRegisteredHotkey(curStr))
                return true;
            // 4. 复用显示/隐藏主界面热键的 RegisterHotKey API 验证：是否被其它应用占用或无效
            if (!IsHotkeyAvailable(rawStr))
            {
                conflictDesc = curStr + " (" + Loc.Get("Settings.Hotkey.AlreadyInUse", "该快捷键已被占用或无效") + ")";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 判断候选热键是否为本应用上次 Apply/启动时已注册的热键（含显示/隐藏主界面与全部全局快捷键）。
        /// 这些热键当前已在系统注册，若不跳过会被 API 测试误判为冲突。
        /// </summary>
        private bool IsOwnRegisteredHotkey(string normalizedCandidate)
        {
            if (string.IsNullOrEmpty(normalizedCandidate)) return false;
            var savedShowHide = HotkeyService.NormalizeHotkey(_originalSettings.Hotkey);
            if (!string.IsNullOrEmpty(savedShowHide) &&
                string.Equals(normalizedCandidate, savedShowHide, StringComparison.OrdinalIgnoreCase))
                return true;
            if (_originalSettings.GlobalHotkeys != null)
            {
                foreach (var g in _originalSettings.GlobalHotkeys)
                {
                    if (g == null) continue;
                    var s = HotkeyService.NormalizeHotkey(g.ToHotkeyString());
                    if (!string.IsNullOrEmpty(s) &&
                        string.Equals(normalizedCandidate, s, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>收集所有全局快捷键条目当前值到设置列表。</summary>
        private void CollectGlobalHotkeyEntries()
        {
            var list = new List<GlobalHotkey>();
            foreach (var child in GlobalHotkeyEntriesPanel.Children)
            {
                if (child is Border entry)
                    list.Add(ReadGlobalHotkeyEntry(entry));
            }
            _settingsService.Settings.GlobalHotkeys = list;
        }

        /// <summary>同步每条全局快捷键条目的「已保存快照」为当前值（应用后调用）。</summary>
        private void SyncGlobalHotkeyEntrySnapshots()
        {
            foreach (var child in GlobalHotkeyEntriesPanel.Children)
            {
                if (child is Border entry)
                    entry.Tag = ReadGlobalHotkeyEntry(entry).Clone();
            }
        }

        #endregion

        #region 快捷命令条目

        private const string QcNameTextBox = "QcNameTextBox";
        private const string QcRulesPanel = "QcRulesPanel";
        private const string QcEnabledCheckBox = "QcEnabledCheckBox";

        // 规则行控件命名模式：前缀 + 规则索引 + 后缀
        private const string QcRuleEnabledSuffix = "_Enabled";
        private const string QcRuleFuncSuffix = "_Func";
        private const string QcRuleP1Suffix = "_P1";
        private const string QcRuleP2Suffix = "_P2";
        private const string QcRuleUpSuffix = "_Up";
        private const string QcRuleDownSuffix = "_Down";
        private const string QcRuleDelSuffix = "_Del";
        private static string RuleCtrlName(int idx, string suffix) => "QcRule_" + idx + suffix;

        /// <summary>从已保存设置加载并构建所有快捷命令条目。</summary>
        private void LoadQuickCommandEntries()
        {
            QuickCommandEntriesPanel.Children.Clear();
            var list = _settingsService.Settings.QuickCommands;
            if (list != null)
            {
                foreach (var qc in list)
                {
                    var cloned = qc.Clone();
                    QuickCommandEntriesPanel.Children.Add(BuildQuickCommandEntry(cloned));
                }
            }
        }

        /// <summary>➕按钮：新增一条默认值快捷命令条目。</summary>
        private void AddQuickCommandEntry_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new QuickCommand
            {
                Name = "",
                ClipboardType = -2, // 文本+富文本，此卡片仅支持这两种类型
                Rules = new System.Collections.Generic.List<QuickCommandRule>
                {
                    new QuickCommandRule
                    {
                        Enabled = true,
                        Function = QuickCommandFunction.StringReplace,
                        Param1 = "",
                        Param2 = ""
                    }
                }
            };
            QuickCommandEntriesPanel.Children.Add(BuildQuickCommandEntry(defaults));
        }

        /// <summary>💡按钮：弹出模板菜单，点击模板项后新增对应的快捷命令条目。</summary>
        private void QuickCommandTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            var menu = new ContextMenu
            {
                Style = TryFindResource("ContextMenuStyle") as Style,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = btn
            };

            foreach (var tpl in GetQuickCommandTemplates())
            {
                var captured = tpl; // 闭包捕获
                var item = new MenuItem
                {
                    Header = captured.Name,
                    Style = TryFindResource("MenuItemStyle") as Style
                };
                item.Click += (s, args) =>
                {
                    QuickCommandEntriesPanel.Children.Add(BuildQuickCommandEntry(captured.Clone()));
                };
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        /// <summary>
        /// 模板预设：点击💡 菜单项后新增的快捷命令模板。
        /// </summary>
        private System.Collections.Generic.List<QuickCommand> GetQuickCommandTemplates()
        {
            return new System.Collections.Generic.List<QuickCommand>
            {
                // 1. 提取全部手机号
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Phone", "提取全部手机号"),
                    ClipboardType = -2, // 仅文本 + 富文本
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,Function = QuickCommandFunction.RegexMatches,Param1 = @"1[3-9](?:[\s\-]?\d){9}",Param2 = ","
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,Function = QuickCommandFunction.RemoveWhiteSpace,Param1 = "",Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,Function = QuickCommandFunction.StringReplace,Param1 = "-",Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,Function = QuickCommandFunction.RegexReplace,Param1 = @"(?<=\d{11})(?=1[3-9]\d{9}(?!\d))",Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.IdCard", "提取全部身份证号"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\b[1-9]\d{5}(?:19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[0-9Xx]\b",
                            Param2 = ","
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.StringReplace,
                            Param1 = " ",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Email", "提取全部邮箱地址"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\b[\w\.\-]+@[\w\-]+(?:\.[\w\-]+)+\b",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Url", "提取全部网址链接"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\b(?:https?://)?(?:www\.)?(?:[a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}|https?://[^\s\)\]\}>]+|\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.NumberSplit", "提取全部数字（逗号分割）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\d+",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.NumberConcat", "提取全部数字（不分割）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"\d+",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.Chinese", "提取全部中文汉字"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexMatches,
                            Param1 = @"[\u4e00-\u9fa5]",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.CollapseSpaces", "清除多余空格"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"[\t]+",
                            Param2 = " "
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"\s{2,}",
                            Param2 = " "
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"^\s+|\s+$",
                            Param2 = ""
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.DistinctLines", "列表去重（换行数据）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"(?m)^\s+|\s+$",
                            Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"(?ms)^(.*?)(?:\r?\n\1)+(?=\r?\n|$)",
                            Param2 = "$1"
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.LinesToComma", "多行转逗号一行"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"\r?\n",
                            Param2 = ","
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.CommaToLines", "逗号转多行（半角/全角逗号）"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"[,，]",
                            Param2 = "\r\n"
                        }
                    }
                },
                new QuickCommand
                {
                    Name = Loc.Get("Settings.QuickCommand.Template.StripHtml", "去除HTML标签"),
                    ClipboardType = -2,
                    Rules = new List<QuickCommandRule>
                    {
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.RegexReplace,
                            Param1 = @"<[^>]+>",
                            Param2 = ""
                        },
                        new QuickCommandRule
                        {
                            Enabled = true,
                            Function = QuickCommandFunction.StringReplace,
                            Param1 = "&nbsp;",
                            Param2 = " "
                        }
                    }
                }
            };
        }

        #region 快捷命令 - 构建 & 校验

        /// <summary>
        /// 给规则行上的 ↑/↓/× 小图标按钮专用的紧凑 Style。
        /// 颜色与 SecondaryButtonStyle 保持一致，但直接在此显式设置资源回退值 + 小 Padding ControlTemplate，
        /// 完全不依赖资源加载时机或 BasedOn，确保任何情况下都有可见的边框/背景/文字。
        /// </summary>
        private Style _compactIconButtonStyle;
        private Style CompactIconButtonStyle
        {
            get
            {
                if (_compactIconButtonStyle != null) return _compactIconButtonStyle;

                // 解析颜色画笔：先 TryFindResource，失败则硬编码回退（亮/暗都能看清）
                Brush buttonBg = TryFindResource("ButtonBackground") as Brush
                                 ?? new SolidColorBrush(Color.FromRgb(243, 244, 246));
                Brush textFg = TryFindResource("TextForeground") as Brush
                               ?? new SolidColorBrush(Color.FromRgb(17, 24, 39));
                Brush borderBrush = TryFindResource("BorderBrush") as Brush
                                    ?? new SolidColorBrush(Color.FromRgb(209, 213, 219));
                Brush hoverBg = TryFindResource("HoverBackground") as Brush
                                ?? new SolidColorBrush(Color.FromRgb(229, 231, 235));

                var style = new Style(typeof(Button));
                // 显式设置默认属性（不走 BasedOn，避免资源失败全部变透明）
                style.Setters.Add(new Setter(Button.BackgroundProperty, buttonBg));
                style.Setters.Add(new Setter(Button.ForegroundProperty, textFg));
                style.Setters.Add(new Setter(Button.BorderBrushProperty, borderBrush));
                style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
                style.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
                style.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.Bold));
                style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
                style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
                style.Setters.Add(new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                style.Setters.Add(new Setter(Button.VerticalContentAlignmentProperty, VerticalAlignment.Center));

                // 自定义 ControlTemplate，Padding 足够小（2 左右）保证 28×28 能放下 14 号粗体字
                var ct = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                borderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));
                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                cp.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, new TemplateBindingExtension(Button.ForegroundProperty));
                borderFactory.AppendChild(cp);
                ct.VisualTree = borderFactory;

                // 鼠标悬停：背景换 HoverBackground
                var trig = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                trig.Setters.Add(new Setter(Button.BackgroundProperty, hoverBg));
                ct.Triggers.Add(trig);
                // 禁用：降低不透明度
                var trigDisabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
                trigDisabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
                ct.Triggers.Add(trigDisabled);

                style.Setters.Add(new Setter(Control.TemplateProperty, ct));
                _compactIconButtonStyle = style;
                return _compactIconButtonStyle;
            }
        }

        /// <summary>
        /// 构造规则按钮的图标 Content。
        /// 显式设置 Foreground（不再依赖 Binding/TemplatedParent），
        /// 使用 14 号粗体 Unicode 字符，保证无论资源加载情况都能清晰显示。
        /// </summary>
        private object BuildRuleButtonIcon(string glyph)
        {
            var fg = TryFindResource("TextForeground") as Brush
                     ?? new SolidColorBrush(Color.FromRgb(17, 24, 39));
            return new TextBlock
            {
                Text = glyph,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = fg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)  // 细微上移，视觉居中
            };
        }

        // 规则按钮字符：使用清晰可辨的 Unicode 符号
        private const string IconArrowUp = "▲";
        private const string IconArrowDown = "▼";
        private const string IconClose = "✕";

        /// <summary>返回 11 个函数的有序定义列表，顺序与用户要求一致。</summary>
        private static readonly QuickCommandFunction[] OrderedFunctions = new[]
        {
            QuickCommandFunction.StringReplace,
            QuickCommandFunction.RegexReplace,
            QuickCommandFunction.RegexMatches,
            QuickCommandFunction.RegexEscape,
            QuickCommandFunction.StringTrim,
            QuickCommandFunction.StringToUpper,
            QuickCommandFunction.StringToLower,
            QuickCommandFunction.RegexSplit,
            QuickCommandFunction.DistinctRemoveDuplicate,
            QuickCommandFunction.RemoveWhiteSpace,
            QuickCommandFunction.RemoveNewLines
        };

        /// <summary>获取函数的本地化显示名。</summary>
        private static string FunctionDisplayName(QuickCommandFunction f)
        {
            switch (f)
            {
                case QuickCommandFunction.StringReplace:          return Loc.Get("Settings.QuickCommand.Function.StringReplace",          "字面查找替换");
                case QuickCommandFunction.RegexReplace:           return Loc.Get("Settings.QuickCommand.Function.RegexReplace",           "正则查找替换");
                case QuickCommandFunction.RegexMatches:           return Loc.Get("Settings.QuickCommand.Function.RegexMatches",           "正则提取匹配");
                case QuickCommandFunction.RegexEscape:            return Loc.Get("Settings.QuickCommand.Function.RegexEscape",            "正则转义文本");
                case QuickCommandFunction.StringTrim:             return Loc.Get("Settings.QuickCommand.Function.StringTrim",             "去除首尾空白");
                case QuickCommandFunction.StringToUpper:          return Loc.Get("Settings.QuickCommand.Function.StringToUpper",          "转大写");
                case QuickCommandFunction.StringToLower:          return Loc.Get("Settings.QuickCommand.Function.StringToLower",          "转小写");
                case QuickCommandFunction.RegexSplit:             return Loc.Get("Settings.QuickCommand.Function.RegexSplit",             "正则分割(换行拼接)");
                case QuickCommandFunction.DistinctRemoveDuplicate:return Loc.Get("Settings.QuickCommand.Function.DistinctRemoveDuplicate","按行去重");
                case QuickCommandFunction.RemoveWhiteSpace:       return Loc.Get("Settings.QuickCommand.Function.RemoveWhiteSpace",       "移除全部空白");
                case QuickCommandFunction.RemoveNewLines:         return Loc.Get("Settings.QuickCommand.Function.RemoveNewLines",         "移除换行");
            }
            return f.ToString();
        }

        /// <summary>返回函数的参数数量：0 / 1(仅Param1) / 2(Param1+Param2)。</summary>
        private static int FunctionParamCount(QuickCommandFunction f)
        {
            switch (f)
            {
                case QuickCommandFunction.StringReplace:
                case QuickCommandFunction.RegexReplace:
                case QuickCommandFunction.RegexMatches: // Param1=Pattern; Param2=多个匹配结果分隔符（留空=直接拼接）
                    return 2;
                case QuickCommandFunction.RegexSplit:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>返回 Param1/Param2 的占位提示或标签文本（本地化）。</summary>
        private static string ParamToolTip(QuickCommandFunction f, int paramIndex)
        {
            if (paramIndex == 1)
            {
                switch (f)
                {
                    case QuickCommandFunction.StringReplace: return Loc.Get("Settings.QuickCommand.Param.Find", "查找串");
                    case QuickCommandFunction.RegexReplace:
                    case QuickCommandFunction.RegexMatches:
                    case QuickCommandFunction.RegexSplit:
                        return Loc.Get("Settings.QuickCommand.Param.Pattern", "正则 Pattern");
                }
            }
            else if (paramIndex == 2)
            {
                switch (f)
                {
                    case QuickCommandFunction.StringReplace: return Loc.Get("Settings.QuickCommand.Param.Replace", "替换串");
                    case QuickCommandFunction.RegexReplace:  return Loc.Get("Settings.QuickCommand.Param.ReplTemplate", "替换模板(支持$1组引用)");
                    case QuickCommandFunction.RegexMatches:  return Loc.Get("Settings.QuickCommand.Param.Separator", "多个结果分隔符(留空=直接拼接)");
                }
            }
            return "";
        }

        /// <summary>构建单条快捷命令条目 UI：名称行 + RulesStackPanel(规则行) + 添加规则按钮 + 重置/删除/确定按钮栏。</summary>
        private Border BuildQuickCommandEntry(QuickCommand saved)
        {
            if (saved == null) saved = new QuickCommand();
            if (saved.Rules == null) saved.Rules = new System.Collections.Generic.List<QuickCommandRule>();
            // 无任何规则：保底一条默认 StringReplace，保证界面上总是能看到一条可编辑行
            if (saved.Rules.Count == 0)
                saved.Rules.Add(new QuickCommandRule { Enabled = true, Function = QuickCommandFunction.StringReplace });

            var entryBorder = new Border
            {
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Background = (Brush)FindResource("InputBackground"),
                Tag = saved.Clone()
            };
            var root = new StackPanel { Margin = new Thickness(0) };
            entryBorder.Child = root;

            const double inputBoxHeight = 36;
            const double ruleBoxHeight = 36;   // 规则行内的输入框稍微紧凑一点
            Brush labelFg = (Brush)FindResource("SecondaryTextForeground");

            // ---------- Row1: 快捷命令名称 ----------
            var row1 = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
            var nameLabel = new TextBlock
            {
                Text = Loc.Get("Settings.QuickCommand.Label.Name", "命令名称："),
                Width = 110,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = labelFg
            };
            DockPanel.SetDock(nameLabel, Dock.Left);
            row1.Children.Add(nameLabel);
            var nameBox = new TextBox
            {
                Name = QcNameTextBox,
                Text = saved.Name ?? "",
                Height = inputBoxHeight,
                Style = (Style)FindResource("InputTextBoxStyle")
            };
            nameBox.TextChanged += (s, e) => ResetEntryBorder(entryBorder);
            row1.Children.Add(nameBox);
            root.Children.Add(row1);

            // ---------- Rules 专用 StackPanel（只放规则行 Grid，不包含添加按钮，所以 Children.Count == 真实规则数） ----------
            var rulesPanel = new StackPanel
            {
                Name = QcRulesPanel,
                Margin = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(rulesPanel);

            // ---------- 添加规则按钮（rulesPanel 外部，避免被算作规则行） ----------
            var addRuleBtn = new Button
            {
                Content = Loc.Get("Settings.QuickCommand.Button.AddRule", "＋ 添加规则"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(110, 0, 0, 8),
                Padding = new Thickness(12, 4, 12, 4)
            };
            addRuleBtn.Click += (s, e) =>
            {
                var newRule = new QuickCommandRule { Enabled = true, Function = QuickCommandFunction.StringReplace };
                var newRow = BuildSingleRuleRow(newRule, rulesPanel, entryBorder, ruleBoxHeight);
                rulesPanel.Children.Add(newRow);
                RefreshRuleArrows(rulesPanel);
                ResetEntryBorder(entryBorder);
            };
            root.Children.Add(addRuleBtn);

            // ---------- 构建已有的规则行 ----------
            foreach (var rule in saved.Rules)
            {
                var r = BuildSingleRuleRow(rule, rulesPanel, entryBorder, ruleBoxHeight);
                rulesPanel.Children.Add(r);
            }
            RefreshRuleArrows(rulesPanel);

            // ---------- Bottom Button Bar: 启用 / 重置 / 删除 / 确定 ----------
            var buttonBar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 4, 0, 0) };

            var confirmBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Confirm", "确定"),
                Style = (Style)FindResource("PrimaryButtonStyle"),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(6, 0, 0, 0)
            };
            confirmBtn.Click += (s, e) => OnQuickCommandConfirm(entryBorder);
            DockPanel.SetDock(confirmBtn, Dock.Right);
            buttonBar.Children.Add(confirmBtn);

            var deleteBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Delete", "删除"),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(6, 0, 0, 0)
            };
            deleteBtn.Click += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
                QuickCommandEntriesPanel.Children.Remove(entryBorder)),
                System.Windows.Threading.DispatcherPriority.Background);
            DockPanel.SetDock(deleteBtn, Dock.Right);
            buttonBar.Children.Add(deleteBtn);

            var resetBtn = new Button
            {
                Content = Loc.Get("Settings.Hotkey.QuickPaste.Reset", "重置"),
                Style = (Style)FindResource("SecondaryButtonStyle")
            };
            resetBtn.Click += (s, e) => OnQuickCommandReset(entryBorder);
            DockPanel.SetDock(resetBtn, Dock.Right);
            buttonBar.Children.Add(resetBtn);

            // 启用 CheckBox：位于重置按钮左边（Dock.Right 逆序，最后添加 = 最左）
            var enabledCb = new CheckBox
            {
                Name = QcEnabledCheckBox,
                Content = Loc.Get("Settings.Entry.Enabled", "启用"),
                IsChecked = saved.Enabled,
                Style = (Style)FindResource("ModernCheckBoxStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            DockPanel.SetDock(enabledCb, Dock.Right);
            buttonBar.Children.Add(enabledCb);

            root.Children.Add(buttonBar);

            return entryBorder;
        }

        /// <summary>
        /// 构建单个规则行：
        /// Grid 列 0: 启用 CheckBox | 1: 函数 ComboBox | 2: Param1 TextBox | 3: Param2 TextBox | 4: 上移/下移/删除按钮
        /// </summary>
        private Grid BuildSingleRuleRow(QuickCommandRule rule, StackPanel rulesPanel, Border entryBorder, double ruleBoxHeight)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });        // 0: Enabled
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // 1: Function 
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 2: Param1
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 3: Param2
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // 4: Actions

            // 0. 启用 CheckBox
            var enabledCb = new CheckBox
            {
                Name = "RuleEnabled",
                IsChecked = rule.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)FindResource("ModernCheckBoxStyle")
            };
            enabledCb.Checked += (s, e) => ResetEntryBorder(entryBorder);
            enabledCb.Unchecked += (s, e) => ResetEntryBorder(entryBorder);
            Grid.SetColumn(enabledCb, 0);
            row.Children.Add(enabledCb);

            // 1. 函数 ComboBox（与其它 ComboBox 统一使用 ModernComboBoxStyle）
            var funcCb = new ComboBox
            {
                Name = "RuleFunc",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Height = ruleBoxHeight,
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            foreach (var fn in OrderedFunctions)
            {
                var item = new ComboBoxItem
                {
                    Tag = fn,
                    Content = FunctionDisplayName(fn)
                };
                funcCb.Items.Add(item);
                if (fn == rule.Function)
                    funcCb.SelectedItem = item;
            }
            Grid.SetColumn(funcCb, 1);
            row.Children.Add(funcCb);

            // 2. Param1 TextBox
            var p1Tb = new TextBox
            {
                Name = "RuleP1",
                Text = rule.Param1 ?? "",
                Height = ruleBoxHeight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Style = (Style)FindResource("InputTextBoxStyle")
            };
            p1Tb.TextChanged += (s, e) => ResetEntryBorder(entryBorder);
            Grid.SetColumn(p1Tb, 2);
            row.Children.Add(p1Tb);

            // 3. Param2 TextBox
            var p2Tb = new TextBox
            {
                Name = "RuleP2",
                Text = rule.Param2 ?? "",
                Height = ruleBoxHeight,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("InputTextBoxStyle")
            };
            p2Tb.TextChanged += (s, e) => ResetEntryBorder(entryBorder);
            Grid.SetColumn(p2Tb, 3);
            row.Children.Add(p2Tb);

            // 4. Actions: 上移 / 下移 / 删除
            var actionsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var upBtn = new Button
            {
                Name = "RuleUp",
                Content = BuildRuleButtonIcon(IconArrowUp),
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                Style = CompactIconButtonStyle,
                ToolTip = Loc.Get("Settings.QuickCommand.ToolTip.MoveUp", "上移规则")
            };
            var downBtn = new Button
            {
                Name = "RuleDown",
                Content = BuildRuleButtonIcon(IconArrowDown),
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                Style = CompactIconButtonStyle,
                ToolTip = Loc.Get("Settings.QuickCommand.ToolTip.MoveDown", "下移规则")
            };
            var delBtn = new Button
            {
                Name = "RuleDel",
                Content = BuildRuleButtonIcon(IconClose),
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                Style = CompactIconButtonStyle,
                ToolTip = Loc.Get("Settings.QuickCommand.ToolTip.Delete", "删除规则")
            };

            upBtn.Click += (s, e) =>
            {
                int idx = rulesPanel.Children.IndexOf(row);
                if (idx <= 0) return;
                rulesPanel.Children.RemoveAt(idx);
                rulesPanel.Children.Insert(idx - 1, row);
                RefreshRuleArrows(rulesPanel);
                ResetEntryBorder(entryBorder);
            };
            downBtn.Click += (s, e) =>
            {
                int idx = rulesPanel.Children.IndexOf(row);
                if (idx < 0 || idx >= rulesPanel.Children.Count - 1) return;
                rulesPanel.Children.RemoveAt(idx);
                rulesPanel.Children.Insert(idx + 1, row);
                RefreshRuleArrows(rulesPanel);
                ResetEntryBorder(entryBorder);
            };
            delBtn.Click += (s, e) =>
            {
                if (rulesPanel.Children.Count <= 1)
                {
                    // 至少保留一行，直接清空该行内容并重置为默认函数
                    if (enabledCb != null) enabledCb.IsChecked = true;
                    funcCb.SelectedIndex = 0; // StringReplace
                    p1Tb.Text = "";
                    p2Tb.Text = "";
                    ApplyParamVisibilityToRuleRow(row, QuickCommandFunction.StringReplace);
                }
                else
                {
                    rulesPanel.Children.Remove(row);
                    RefreshRuleArrows(rulesPanel);
                }
                ResetEntryBorder(entryBorder);
            };

            actionsPanel.Children.Add(upBtn);
            actionsPanel.Children.Add(downBtn);
            actionsPanel.Children.Add(delBtn);
            Grid.SetColumn(actionsPanel, 4);
            row.Children.Add(actionsPanel);

            // 函数切换时：刷新 Param1 / Param2 的可见性与 Tooltip
            void FuncChanged(object sender, SelectionChangedEventArgs e)
            {
                if (funcCb.SelectedItem is ComboBoxItem cbi && cbi.Tag is QuickCommandFunction fn)
                {
                    ApplyParamVisibilityToRuleRow(row, fn);
                    ResetEntryBorder(entryBorder);
                }
            }
            funcCb.SelectionChanged += FuncChanged;
            // 初次应用一次（基于 rule.Function 已设置的 SelectedItem）
            ApplyParamVisibilityToRuleRow(row, rule.Function);

            return row;
        }

        /// <summary>根据函数调整 Param1/Param2 的 Visibility / Tooltip，隐藏时清空内容避免残留无效值。</summary>
        private static void ApplyParamVisibilityToRuleRow(Grid row, QuickCommandFunction fn)
        {
            int need = FunctionParamCount(fn);
            TextBox p1 = null, p2 = null;
            foreach (var child in row.Children)
            {
                if (child is TextBox tb)
                {
                    if (tb.Name == "RuleP1") p1 = tb;
                    else if (tb.Name == "RuleP2") p2 = tb;
                }
            }
            if (p1 != null)
            {
                bool show = need >= 1;
                p1.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                p1.ToolTip = show ? ParamToolTip(fn, 1) : null;
                if (!show) p1.Text = "";
            }
            if (p2 != null)
            {
                bool show = need >= 2;
                p2.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                p2.ToolTip = show ? ParamToolTip(fn, 2) : null;
                if (!show) p2.Text = "";
            }
        }

        /// <summary>
        /// 根据 rulesPanel 当前顺序刷新每条规则的 ↑/↓ 可用性。
        /// 首行：↑ 禁用；尾行：↓ 禁用；只有一行：两个都禁用。
        /// </summary>
        private static void RefreshRuleArrows(Panel rulesPanel)
        {
            int count = rulesPanel.Children.Count;
            for (int i = 0; i < count; i++)
            {
                if (!(rulesPanel.Children[i] is Grid row)) continue;
                Button up = null, down = null;
                foreach (var child in row.Children)
                {
                    if (child is WrapPanel wp)
                    {
                        foreach (var inner in wp.Children)
                        {
                            if (inner is Button btn)
                            {
                                if (btn.Name == "RuleUp") up = btn;
                                else if (btn.Name == "RuleDown") down = btn;
                            }
                        }
                    }
                }
                if (up != null) up.IsEnabled = count > 1 && i > 0;
                if (down != null) down.IsEnabled = count > 1 && i < count - 1;
            }
        }

        /// <summary>确定按钮：校验名称 + 每条启用规则的必填参数 / 正则语法。通过则绿框，否则弹框。</summary>
        private void OnQuickCommandConfirm(Border entry)
        {
            ValidateQuickCommandEntry(entry, showMessage: true);
        }

        /// <summary>校验快捷命令条目：通过则绿框。showMessage=true 时弹框提醒错误。</summary>
        private bool ValidateQuickCommandEntry(Border entry, bool showMessage)
        {
            var current = ReadQuickCommandEntry(entry);

            // 1. 名称必填
            if (string.IsNullOrWhiteSpace(current.Name))
            {
                ResetEntryBorder(entry);
                if (showMessage)
                {
                    MessageBox.Show(Loc.Get("Settings.QuickCommand.Error.NameEmpty", "快捷命令名称不能为空。"),
                        Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }
            // 2. 名称不重复（排除自己）
            foreach (var child in QuickCommandEntriesPanel.Children)
            {
                if (child == entry || !(child is Border other)) continue;
                var otherQc = ReadQuickCommandEntry(other);
                if (string.Equals(otherQc.Name?.Trim(), current.Name.Trim(), StringComparison.Ordinal))
                {
                    ResetEntryBorder(entry);
                    if (showMessage)
                    {
                        MessageBox.Show(
                            string.Format(Loc.Get("Settings.QuickCommand.Error.NameDuplicate", "快捷命令名称已存在：{0}"), current.Name),
                            Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return false;
                }
            }
            // 3. 校验每条启用规则
            if (current.Rules != null)
            {
                for (int i = 0; i < current.Rules.Count; i++)
                {
                    var r = current.Rules[i];
                    if (r == null || !r.Enabled) continue;
                    string ruleLabel = string.Format(
                        Loc.Get("Settings.QuickCommand.Error.RuleN", "规则 #{0} [{1}]"),
                        (i + 1), FunctionDisplayName(r.Function));
                    int need = FunctionParamCount(r.Function);
                    if (need >= 1 && string.IsNullOrEmpty(r.Param1))
                    {
                        ResetEntryBorder(entry);
                        if (showMessage)
                        {
                            string msg = string.Format(
                                Loc.Get("Settings.QuickCommand.Error.Param1Required", "{0} 的参数一（{1}）不能为空。"),
                                ruleLabel, ParamToolTip(r.Function, 1));
                            MessageBox.Show(msg, Loc.Get("Common.Error", "错误"),
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        return false;
                    }
                    if (need >= 2 && string.IsNullOrWhiteSpace(r.Param2) &&
                        (r.Function == QuickCommandFunction.StringReplace ||
                         r.Function == QuickCommandFunction.RegexReplace))
                    {
                        // 注意：RegexReplace 的替换串允许为空（等价于删除匹配），所以这里不强制 Param2 非空
                        // 仅 StringReplace：空的替换串和"删除"语义一致，也允许空
                    }
                    // 正则语法校验：RegexReplace / RegexMatches / RegexSplit 都需要
                    if (r.Function == QuickCommandFunction.RegexReplace ||
                        r.Function == QuickCommandFunction.RegexMatches ||
                        r.Function == QuickCommandFunction.RegexSplit)
                    {
                        try
                        {
                            _ = new System.Text.RegularExpressions.Regex(r.Param1 ?? "");
                        }
                        catch (Exception ex)
                        {
                            ResetEntryBorder(entry);
                            if (showMessage)
                            {
                                string msg = string.Format(
                                    Loc.Get("Settings.QuickCommand.Error.RuleRegexInvalid",
                                        "{0} 正则 Pattern 无效：{1}"),
                                    ruleLabel, ex.Message);
                                MessageBox.Show(msg, Loc.Get("Common.Error", "错误"),
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            return false;
                        }
                    }
                }
            }

            // 校验通过：绿框
            SetEntryConfirmedBorder(entry);
            return true;
        }

        /// <summary>重置按钮：恢复该条目到已保存快照。</summary>
        private void OnQuickCommandReset(Border entry)
        {
            var saved = entry.Tag as QuickCommand;
            if (saved == null) return;
            var rebuilt = BuildQuickCommandEntry(saved.Clone());
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var idx = QuickCommandEntriesPanel.Children.IndexOf(entry);
                if (idx >= 0)
                {
                    QuickCommandEntriesPanel.Children.RemoveAt(idx);
                    QuickCommandEntriesPanel.Children.Insert(idx, rebuilt);
                    // 重置后重新校验，恢复绿框（若快照值仍合法）
                    ValidateQuickCommandEntry(rebuilt, showMessage: false);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion

        #region 快捷命令 - 读取与收集

        /// <summary>枚举任意容器的直接子元素（Panel / Decorator / Border / ContentControl）。</summary>
        private static System.Collections.Generic.IEnumerable<System.Windows.DependencyObject> EnumerateDirectChildren(
            System.Windows.DependencyObject parent)
        {
            if (parent == null) yield break;

            // 优先 Panel.Children（DockPanel / WrapPanel / StackPanel / Grid 等）
            if (parent is Panel p)
            {
                foreach (System.Windows.UIElement c in p.Children)
                    yield return c;
                yield break;
            }

            // Decorator 单内容容器（Border 等）
            if (parent is Decorator dec && dec.Child != null)
            {
                yield return dec.Child;
                yield break;
            }

            // ContentControl 单内容控件
            if (parent is ContentControl cc && cc.Content is System.Windows.DependencyObject ccChild)
            {
                yield return ccChild;
                yield break;
            }
        }

        /// <summary>在指定父容器内按 Name 递归查找指定类型的第一个子控件。</summary>
        private static T FindQcChildByName<T>(System.Windows.DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            foreach (var c in EnumerateDirectChildren(parent))
            {
                if (c is T typed && string.Equals(typed.Name, name, StringComparison.Ordinal))
                    return typed;
                var nested = FindQcChildByName<T>(c, name);
                if (nested != null) return nested;
            }
            return null;
        }

        /// <summary>从单个规则行 Grid 中读取控件值生成 QuickCommandRule。</summary>
        private static QuickCommandRule ReadRuleRow(Grid rowGrid)
        {
            var rule = new QuickCommandRule { Enabled = true };

            var enabled = FindQcChildByName<CheckBox>(rowGrid, "RuleEnabled");
            if (enabled != null) rule.Enabled = enabled.IsChecked == true;

            var funcCb = FindQcChildByName<ComboBox>(rowGrid, "RuleFunc");
            if (funcCb?.SelectedItem is ComboBoxItem cbi && cbi.Tag is QuickCommandFunction fn)
                rule.Function = fn;

            var p1 = FindQcChildByName<TextBox>(rowGrid, "RuleP1");
            if (p1 != null) rule.Param1 = p1.Text ?? "";

            var p2 = FindQcChildByName<TextBox>(rowGrid, "RuleP2");
            if (p2 != null) rule.Param2 = p2.Text ?? "";

            return rule;
        }

        /// <summary>读取单条快捷命令条目的当前控件值（Name + 规则链 + 启用状态）。</summary>
        private QuickCommand ReadQuickCommandEntry(Border entry)
        {
            var qc = new QuickCommand();
            qc.ClipboardType = -2;
            qc.Rules = new System.Collections.Generic.List<QuickCommandRule>();

            var tbName = FindQcChildByName<TextBox>(entry, QcNameTextBox);
            if (tbName != null) qc.Name = tbName.Text?.Trim() ?? "";

            var rulesPanel = FindQcChildByName<StackPanel>(entry, QcRulesPanel);
            if (rulesPanel != null)
            {
                // 注意：rulesPanel 只包含规则行 Grid（添加规则按钮在外面，不会混入）
                foreach (var child in rulesPanel.Children)
                {
                    if (child is Grid rowGrid)
                        qc.Rules.Add(ReadRuleRow(rowGrid));
                }
            }

            // 读取启用 CheckBox
            var enabledCb = FindQcChildByName<CheckBox>(entry, QcEnabledCheckBox);
            if (enabledCb != null) qc.Enabled = enabledCb.IsChecked == true;

            return qc;
        }

        /// <summary>收集所有条目当前值到设置列表。</summary>
        private void CollectQuickCommandEntries()
        {
            var list = new List<QuickCommand>();
            foreach (var child in QuickCommandEntriesPanel.Children)
            {
                if (child is Border entry)
                    list.Add(ReadQuickCommandEntry(entry));
            }
            _settingsService.Settings.QuickCommands = list;
        }

        /// <summary>应用按钮后同步「已保存快照」。</summary>
        private void SyncQuickCommandEntrySnapshots()
        {
            foreach (var child in QuickCommandEntriesPanel.Children)
            {
                if (child is Border entry)
                    entry.Tag = ReadQuickCommandEntry(entry).Clone();
            }
        }

        #endregion
        #endregion
    }
}
