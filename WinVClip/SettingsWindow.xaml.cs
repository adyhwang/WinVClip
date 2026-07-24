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
                UseSystemCharPanel = _settingsService.Settings.UseSystemCharPanel
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
            ApplyPanelCaptureLocalization();
            ApplyPanelHistoryLocalization();
            ApplyPanelStorageLocalization();
            ApplyPanelSearchLocalization();
            ApplyPanelSystemLocalization();
        }

        private void ApplyPanelGeneralLocalization()
        {
            GeneralTitleText.Text = Loc.Get("Settings.General.Title", "常规");
            HotkeyCardTitle.Text = Loc.Get("Settings.General.Hotkey.Title", "快捷键");
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

            if (hotkeyChanged)
            {
                App.UpdateHotkey(_settingsService.Settings.Hotkey);
            }

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
                
                if (StartWithWindowsCheckBox != null)
                {
                    StartWithWindowsCheckBox.IsChecked = false;
                }
                
                DataContext = null;
                DataContext = _settingsService.Settings;
                
                UpdateCustomSearchEngineGridVisibility();
                
                MessageBox.Show(Loc.Get("Settings.Reset.Success", "设置已重置为默认值。点击\"应用\"或\"确定\"保存更改。"), Loc.Get("Settings.Reset.SuccessTitle", "重置成功"), 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
