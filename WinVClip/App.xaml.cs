using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class App : Application
    {
        private static DatabaseService? _databaseService;
        private static SettingsService? _settingsService;
        private static TrayService? _trayService;
        private static HotkeyService? _hotkeyService;
        private static ClipboardMonitor? _clipboardMonitor;
        private static CleanupService? _cleanupService;
        private static MainWindow? _mainWindow;
        private static SettingsWindow? _settingsWindow;
        private static Mutex? _mutex;
        private static FocusService? _focusService;
        private static WindowStateService? _windowStateService;
        private static System.Threading.Timer? _vacuumTimer;
        private static System.Threading.Timer? _checkpointTimer;

        public static DatabaseService DatabaseService => _databaseService ??= new DatabaseService(GetDatabasePath());
        public static SettingsService SettingsService => _settingsService ??= new SettingsService();
        public static FocusService FocusService => _focusService ??= new FocusService();
        public static WindowStateService WindowStateService => _windowStateService ??= new WindowStateService();

        private static string GetDatabasePath()
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                    if (!string.IsNullOrEmpty(settings?.DatabasePath))
                        return settings.DatabasePath;
                }
                catch
                {
                }
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clipboard_history.db");
        }

        private const int WM_SHOWMAINWINDOW = 0x0400 + 0x0001;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "WinVClip_Mutex", out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();

                var existingHwnd = FindWindow(null, "WinVClip");
                if (existingHwnd != IntPtr.Zero)
                {
                    PostMessage(existingHwnd, WM_SHOWMAINWINDOW, IntPtr.Zero, IntPtr.Zero);
                    SetForegroundWindow(existingHwnd);
                }

                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            _settingsService = SettingsService;

            LocalizationService.Instance.Initialize(_settingsService.Settings.Language);
            if (string.IsNullOrEmpty(_settingsService.Settings.Language))
            {
                _settingsService.Settings.Language = LocalizationService.Instance.CurrentLanguageCode;
                _settingsService.SaveSettings();
            }

            if (_settingsService.Settings.IsAdministratorRun && !IsAdministrator())
            {
                TryRestartAsAdministrator();
                _mutex.Dispose();
                Current.Shutdown();
                return;
            }

            _settingsService.StartupTask.CheckAndUpdatePath();

            _databaseService = DatabaseService;

            ThemeService.Instance.Initialize(_settingsService.Settings);

            _focusService = FocusService;
            _focusService.StartMonitoring();

            _windowStateService = WindowStateService;

            _mainWindow = new MainWindow(_databaseService, _settingsService);
            // 显式绑定 Application.MainWindow：窗口启动时从未 Show()，
            // WPF 不会自动设置该属性，导致托盘右键菜单的 PlacementTarget 为 null 无法弹出。
            Application.Current.MainWindow = _mainWindow;
            
            var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
            windowInteropHelper.EnsureHandle();
            var windowHandle = windowInteropHelper.Handle;
            _focusService.AddExcludedHwnd(windowHandle);
            _hotkeyService = new HotkeyService(windowHandle);
            // 启动时注册全部热键（显示/隐藏主界面 + 全局快捷键），返回显示/隐藏热键是否注册成功
            var hotkeyRegistered = RegisterAllHotkeys();

            _trayService = new TrayService(_settingsService, windowHandle);
            _trayService.OnShowWindow += () => Dispatcher.Invoke(() => _mainWindow?.ShowAtCursor());
            _trayService.OnOpenSettings += () => Dispatcher.Invoke(() => ShowSettingsWindow());
            _trayService.OnMonitoringToggled += (enabled) => Dispatcher.Invoke(() => UpdateMonitoring(enabled));
            _trayService.OnExit += () => 
            {
                Dispatcher.Invoke(() => 
                {
                    _mainWindow?.Close();
                    Current.Shutdown();
                });
            };

            if (!hotkeyRegistered)
            {
                _trayService.ShowNotification(
                    Loc.Get("App.HotkeyRegisterFailed"),
                    Loc.Get("App.HotkeyRegisterFailedMessage"));
            }
            else if (_settingsService.Settings.ShowHotkeyTipOnStartup)
            {
                _trayService.ShowNotification(
                    Loc.Get("App.Started"),
                    string.Format(Loc.Get("App.StartedMessage", "Hotkey {0} registered"), _settingsService.Settings.Hotkey));
            }

            _clipboardMonitor = new ClipboardMonitor(_databaseService, _settingsService);
            _clipboardMonitor.OnClipboardChanged += item => 
            {
                // 主窗口显示与否都把 item 写入 ViewModel：
                // - 显示时：立即插入列表顶部。
                // - 隐藏时：同步更新集合与计数，保证再次 Show 时数据已是最新，
                //   无需依赖用户点过滤/分组才触发 LoadItems 刷新。
                Dispatcher.Invoke(() =>
                {
                    if (_mainWindow?.ViewModel != null)
                        _mainWindow.ViewModel.AddItem(item);
                });
            };
            _clipboardMonitor.OnDuplicateUpdated += () => 
            {
                if (_mainWindow?.ViewModel != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_mainWindow?.ViewModel != null)
                            _mainWindow.ViewModel.LoadItems();
                    });
                }
            };
            _clipboardMonitor.Start();

            _cleanupService = new CleanupService(_databaseService);
            if (_settingsService.Settings.EnableAutoCleanup)
            {
                _cleanupService.Start(_settingsService.Settings.RetentionDays);
            }

            // 预热首次渲染：窗口启动时仅通过 EnsureHandle 创建了 HWND，从未真正 Show()，
            // 首次快捷键弹出时 WPF 需要完成首次布局、样式实例化、模板构建及 JIT 编译，导致明显卡顿。
            // 此处以透明无激活方式提前完成预热，消除首次弹出卡顿。
            _mainWindow.PreheatRender();

            _mainWindow.HideWindow();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                CheckAndPerformPeriodicVacuum();

                // 每 6 小时执行一次 WAL checkpoint（轻量操作，截断 .db-wal 文件）
                _checkpointTimer = new System.Threading.Timer(
                    callback: _ => Dispatcher.Invoke(() => _databaseService?.CheckpointDatabase()),
                    state: null,
                    dueTime: TimeSpan.FromHours(6),
                    period: TimeSpan.FromHours(6));

                // 每 1 天检查一次是否需要 VACUUM（实际每 2 天执行一次完整压缩）
                // 必须在 Dispatcher 上执行，避免与剪贴板监控的写入并发访问同一连接
                _vacuumTimer = new System.Threading.Timer(
                    callback: _ => Dispatcher.Invoke(() => CheckAndPerformPeriodicVacuum()),
                    state: null,
                    dueTime: TimeSpan.FromDays(1),
                    period: TimeSpan.FromDays(1));
            }), System.Windows.Threading.DispatcherPriority.Background, null);

            Current.Exit += (s, args) => CleanupResources();
        }

        private static void CleanupResources()
        {
            _vacuumTimer?.Dispose();
            _checkpointTimer?.Dispose();
            _clipboardMonitor?.Stop();
            _clipboardMonitor?.Dispose();
            _hotkeyService?.Dispose();
            _cleanupService?.Dispose();
            _trayService?.Dispose();
            // 退出前主动 checkpoint，把 WAL 内容合并回主库并截断 .db-wal 文件
            _databaseService?.CheckpointDatabase();
            _databaseService?.Dispose();
            _focusService?.Dispose();
            _mutex?.Dispose();
        }

        public static void ShowSettingsWindow()
        {
            if (_settingsWindow == null || !_settingsWindow.IsVisible)
            {
                _settingsWindow = new SettingsWindow(SettingsService);
                _settingsWindow.Owner = _mainWindow;
                _settingsWindow.ShowDialog();
                _settingsWindow = null;
            }
        }

        /// <summary>
        /// 重新注册全部热键：先注销所有，再依次注册「显示/隐藏主界面」热键与全部「全局快捷键」。
        /// 返回显示/隐藏主界面热键是否注册成功（用于启动通知）。
        /// </summary>
        public static bool RegisterAllHotkeys()
        {
            if (_hotkeyService == null) return false;
            _hotkeyService.UnregisterAll();

            bool showHideOk = false;
            var settings = _settingsService?.Settings;
            if (settings != null)
            {
                // 1. 显示/隐藏主界面
                var hk = settings.Hotkey;
                if (!string.IsNullOrWhiteSpace(hk))
                {
                    showHideOk = _hotkeyService.RegisterHotkey(hk, () =>
                        Current.Dispatcher.Invoke(() => _mainWindow?.ToggleVisibility()));
                }

                // 2. 全局快捷键：修饰键+按键 → 对第 N 项剪贴板条目执行操作
                var list = settings.GlobalHotkeys;
                if (list != null)
                {
                    foreach (var gh in list)
                    {
                        if (gh == null) continue;
                        if (!gh.Enabled) continue;
                        var captured = gh;
                        var hotkeyStr = gh.ToHotkeyString();
                        if (string.IsNullOrWhiteSpace(hotkeyStr)) continue;
                        _hotkeyService.RegisterHotkey(hotkeyStr, () =>
                            Current.Dispatcher.Invoke(() => _mainWindow?.ExecuteGlobalHotkeyAction(captured)));
                    }
                }
            }
            return showHideOk;
        }

        /// <summary>WndProc 收到 WM_HOTKEY 时调用，按注册的 id 分发到对应动作。</summary>
        public static void ProcessHotkey(int id)
        {
            _hotkeyService?.ProcessHotkey(id);
        }

        /// <summary>兼容旧调用：显示/隐藏主界面热键变更时，统一重新注册全部热键。</summary>
        public static void UpdateHotkey(string hotkey)
        {
            RegisterAllHotkeys();
        }

        public static void UpdateMonitoring(bool enabled)
        {
            if (_clipboardMonitor != null)
            {
                if (enabled)
                {
                    _clipboardMonitor.Start();
                }
                else
                {
                    _clipboardMonitor.Stop();
                }
            }
        }

        public static void UpdateCleanup(bool enabled, int days)
        {
            if (_cleanupService != null)
            {
                if (enabled)
                {
                    _cleanupService.Start(days);
                }
                else
                {
                    _cleanupService.Stop();
                }
            }
        }

        public static MainWindow? GetMainWindow() => _mainWindow;

        public static ClipboardMonitor? GetClipboardMonitor() => _clipboardMonitor;

        public static FocusService? GetFocusService() => _focusService;

        public static WindowStateService? GetWindowStateService() => _windowStateService;

        public static void RecreateTrayIcon()
        {
            // 释放旧的托盘图标
            _trayService?.Dispose();

            // 重新创建托盘图标
            if (_settingsService != null && _mainWindow != null)
            {
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
                _trayService = new TrayService(_settingsService, windowHandle);
                _trayService.OnShowWindow += () => Current?.Dispatcher.Invoke(() => _mainWindow?.ShowAtCursor());
                _trayService.OnOpenSettings += () => Current?.Dispatcher.Invoke(() => ShowSettingsWindow());
                _trayService.OnMonitoringToggled += (enabled) => Current?.Dispatcher.Invoke(() => UpdateMonitoring(enabled));
                _trayService.OnExit += () =>
                {
                    Current?.Dispatcher.Invoke(() =>
                    {
                        _mainWindow?.Close();
                        Current?.Shutdown();
                    });
                };
            }
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

        private void TryRestartAsAdministrator()
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                        ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(processInfo);
            }
            catch
            {
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupResources();
            base.OnExit(e);
        }

        private void CheckAndPerformPeriodicVacuum()
        {
            try
            {
                var today = DateTime.Now.Date;
                var lastVacuumDate = _settingsService?.Settings?.LastVacuumDate?.Date ?? DateTime.MinValue;
                var daysSinceLastVacuum = (today - lastVacuumDate).TotalDays;

                // 每两天执行一次 Vacuum
                if (daysSinceLastVacuum >= 2)
                {
                    System.Diagnostics.Debug.WriteLine($"[Vacuum] 开始执行数据库压缩，上次执行: {lastVacuumDate:yyyy-MM-dd}");

                    // 必须在 UI 线程执行，避免与剪贴板监控并发访问同一 SqliteConnection
                    try
                    {
                        _databaseService?.VacuumDatabase();
                        if (_settingsService?.Settings != null)
                        {
                            _settingsService.Settings.LastVacuumDate = today;
                            _settingsService.SaveSettings();
                            System.Diagnostics.Debug.WriteLine($"[Vacuum] 数据库压缩成功，已更新日期: {today:yyyy-MM-dd}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Vacuum] 数据库压缩失败: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Vacuum] 距离上次压缩 {daysSinceLastVacuum:F1} 天，无需执行");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Vacuum] 检查压缩状态失败: {ex.Message}");
            }
        }
    }
}
