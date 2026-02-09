using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
            // 从注册表读取开机启动状态
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
                MaxHistoryItems = _settingsService.Settings.MaxHistoryItems
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
            
            // 设置开机启动复选框状态（从注册表读取）
            if (StartWithWindowsCheckBox != null)
            {
                StartWithWindowsCheckBox.IsChecked = _originalStartWithWindows;
            }

            _settingsService.InitializeSearchEngines();
            
            if (CustomSearchEngineGrid != null)
            {
                UpdateCustomSearchEngineGridVisibility();
            }
        }

        private void UpdateCustomSearchEngineGridVisibility()
        {
            if (CustomSearchEngineGrid == null)
                return;
                
            var selectedId = _settingsService.Settings.SelectedSearchEngineId;
            CustomSearchEngineGrid.Visibility = selectedId == "custom" ? Visibility.Visible : Visibility.Collapsed;
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
            HotkeyTextBox.Text = "按下快捷键组合...";
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
                if (string.IsNullOrWhiteSpace(newHotkey) || newHotkey == "按下快捷键组合...")
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
                    MessageBox.Show("快捷键格式不正确。支持的格式：\n• 修饰键+按键（如 Ctrl+V、Alt+F1）\n• 单独的功能键 F1-F12", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("该快捷键已被占用或无效", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    "该快捷键与系统快捷键冲突，需要管理员权限才能修改注册表并禁用系统的历史剪贴板。\n\n是否以管理员身份重启程序？",
                    "需要管理员权限",
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
                "该快捷键与系统快捷键冲突，需要在注册表禁用系统的历史剪贴板并重启资源管理器。\n\n是否继续？",
                "快捷键冲突",
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
                MessageBox.Show($"启动管理员权限失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

                MessageBox.Show("系统 Win+V 已禁用，资源管理器已重启。\n\n现在可以使用 Win+V 作为快捷键了。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"禁用系统 Win+V 失败: {ex.Message}\n\n请以管理员身份运行程序。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("该快捷键已经是当前设置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 验证快捷键格式
                if (!HotkeyService.ValidateHotkey(hotkey))
                {
                    MessageBox.Show("快捷键格式不正确。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查 Win+V 冲突
                if (!CheckAndHandleWinVConflict(hotkey))
                    return;

                // 检查是否已被占用
                if (!IsHotkeyAvailable(hotkey))
                {
                    MessageBox.Show("该快捷键已被占用或无效", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 应用快捷键
                _settingsService.Settings.Hotkey = hotkey;
                _tempHotkey = hotkey;
                HotkeyTextBox.Text = hotkey;
                App.UpdateHotkey(hotkey);

                MessageBox.Show($"快捷键已设置为 {hotkey}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RestoreSystemWinV_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有管理员权限
            if (!IsAdministrator())
            {
                var result = MessageBox.Show(
                    "还原系统 Win+V 需要管理员权限才能修改注册表。\n\n是否以管理员身份重启程序？",
                    "需要管理员权限",
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

                // 重启资源管理器
                RestartExplorer();

                MessageBox.Show("系统 Win+V 已还原，资源管理器已重启。\n\n现在可以使用系统历史剪贴板功能了。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"还原系统 Win+V 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseDatabasePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "数据库文件 (*.db)|*.db|所有文件 (*.*)|*.*",
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
                Filter = "数据库文件 (*.db)|*.db",
                DefaultExt = ".db",
                FileName = $"clipboard_history_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sourcePath = _settingsService.Settings.DatabasePath;
                    if (System.IO.File.Exists(sourcePath))
                    {
                        System.IO.File.Copy(sourcePath, dialog.FileName, true);
                        MessageBox.Show("数据库备份成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("数据库文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"备份失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = $"clipboard_export_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var items = App.DatabaseService.GetAllItems();
                    var lines = new List<string>();
                    
                    foreach (var item in items)
                    {
                        if (item.Type == ClipboardType.Text && !string.IsNullOrWhiteSpace(item.Content))
                        {
                            lines.Add(item.Content);
                        }
                    }
                    
                    System.IO.File.WriteAllLines(dialog.FileName, lines);
                    MessageBox.Show($"导出成功！共导出 {lines.Count} 条文本记录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                // 创建进度窗口
                var progressWindow = new Window
                {
                    Title = "导入数据",
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
                    Text = "正在读取文件...",
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
                        var processedCount = 0;
                        var skippedCount = 0;
                        var deletedCount = 0;

                        // 计算有效行数
                        var validLines = lines.Where(line => !string.IsNullOrWhiteSpace(line.Trim())).Count();

                        await Dispatcher.InvokeAsync(() =>
                        {
                            statusText.Text = $"正在检查重复数据...";
                            progressBar.Maximum = totalLines;
                            detailText.Text = $"文件共 {totalLines} 行，有效数据约 {validLines} 条";
                            progressWindow.Show();
                        });

                        // 第一阶段：处理重复数据
                        // 策略：保留已分组的，删除未分组的，如果已分组则跳过导入
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedLine))
                            {
                                var (exists, isGrouped) = App.DatabaseService.CheckContentExists(trimmedLine, (int)ClipboardType.Text);
                                if (exists)
                                {
                                    if (isGrouped)
                                    {
                                        // 已分组：保留，跳过导入
                                        skippedCount++;
                                    }
                                    else
                                    {
                                        // 未分组：删除旧数据，导入新数据
                                        var deleted = App.DatabaseService.DeleteUngroupedDuplicates(trimmedLine, (int)ClipboardType.Text);
                                        deletedCount += deleted;
                                    }
                                }
                            }
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            var infoText = "";
                            if (deletedCount > 0)
                                infoText += $"已清理 {deletedCount} 条未分组重复数据。";
                            if (skippedCount > 0)
                                infoText += $"已跳过 {skippedCount} 条已分组数据。";
                            detailText.Text = infoText;
                            statusText.Text = $"正在导入数据 (0/{validLines})...";
                        });

                        // 第二阶段：从最后一行开始导入到第一行（倒序导入）
                        for (int i = lines.Length - 1; i >= 0; i--)
                        {
                            processedCount++;
                            var trimmedLine = lines[i].Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedLine))
                            {
                                // 检查是否已存在（包括已分组的）
                                var (exists, isGrouped) = App.DatabaseService.CheckContentExists(trimmedLine, (int)ClipboardType.Text);
                                if (exists)
                                {
                                    // 已存在（无论是分组还是未分组），都不导入
                                    continue;
                                }

                                var item = new ClipboardItem
                                {
                                    Type = ClipboardType.Text,
                                    Content = trimmedLine,
                                    CreatedAt = DateTime.Now,
                                    PreviewText = trimmedLine.Length > 100 ? trimmedLine.Substring(0, 100) : trimmedLine
                                };
                                App.DatabaseService.InsertItem(item);
                                importCount++;
                            }

                            // 每处理 10 条或最后一条时更新进度
                            if (processedCount % 10 == 0 || i == 0)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    progressBar.Value = processedCount;
                                    statusText.Text = $"正在导入数据 ({importCount}/{validLines})...";
                                    if (importCount > 0 && trimmedLine.Length > 0)
                                    {
                                        var preview = trimmedLine.Length > 30 ? trimmedLine.Substring(0, 30) + "..." : trimmedLine;
                                        detailText.Text = $"当前: {preview}";
                                    }
                                });
                            }

                            // 每 50 条暂停一下，避免阻塞 UI
                            if (processedCount % 50 == 0)
                            {
                                await Task.Delay(1);
                            }
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow.Close();
                            var message = $"导入成功！共导入 {importCount} 条记录。";
                            if (deletedCount > 0)
                                message += $"\n已清理 {deletedCount} 条未分组重复数据。";
                            if (skippedCount > 0)
                                message += $"\n已跳过 {skippedCount} 条已分组重复数据（保留）。";
                            MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow.Close();
                            MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            var monitoringChanged = _settingsService.Settings.MonitorEnabled != _originalSettings.MonitorEnabled;
            var cleanupChanged = _settingsService.Settings.EnableAutoCleanup != _originalSettings.EnableAutoCleanup ||
                                _settingsService.Settings.RetentionDays != _originalSettings.RetentionDays;
            var themeChanged = _settingsService.Settings.Theme != _originalSettings.Theme;
            // 从CheckBox获取开机启动状态
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

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var hotkeyChanged = _settingsService.Settings.Hotkey != _originalSettings.Hotkey;
            var cleanupChanged = _settingsService.Settings.EnableAutoCleanup != _originalSettings.EnableAutoCleanup ||
                                _settingsService.Settings.RetentionDays != _originalSettings.RetentionDays;
            var themeChanged = _settingsService.Settings.Theme != _originalSettings.Theme;
            // 从CheckBox获取当前开机启动状态
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
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要重置所有设置吗？这将恢复到默认配置。", "确认重置", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                // 重置为默认值
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
                
                // 重置开机启动复选框（UI状态，不保存到settings.json）
                if (StartWithWindowsCheckBox != null)
                {
                    StartWithWindowsCheckBox.IsChecked = false;
                }
                
                // 刷新UI
                DataContext = null;
                DataContext = _settingsService.Settings;
                
                // 更新自定义搜索引擎显示
                UpdateCustomSearchEngineGridVisibility();
                
                MessageBox.Show("设置已重置为默认值。点击\"应用\"或\"确定\"保存更改。", "重置成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
