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

            _databaseService = DatabaseService;

            ThemeService.Instance.Initialize(_settingsService.Settings);

            _focusService = FocusService;
            _focusService.StartMonitoring();

            _windowStateService = WindowStateService;

            _mainWindow = new MainWindow(_databaseService, _settingsService);
            _mainWindow.Show();
            
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            _focusService.AddExcludedHwnd(windowHandle);
            _hotkeyService = new HotkeyService(windowHandle);
            var hotkeyRegistered = _hotkeyService.RegisterHotkey(_settingsService.Settings.Hotkey, () => 
            {
                Dispatcher.Invoke(() => _mainWindow?.ToggleVisibility());
            });

            _trayService = new TrayService(_settingsService, windowHandle);
            _trayService.SetMainWindow(_mainWindow);
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
            else
            {
                _trayService.ShowNotification(
                    Loc.Get("App.Started"),
                    string.Format(Loc.Get("App.StartedMessage", "Hotkey {0} registered"), _settingsService.Settings.Hotkey));
            }

            _clipboardMonitor = new ClipboardMonitor(_databaseService, _settingsService);
            _clipboardMonitor.OnClipboardChanged += item => 
            {
                Dispatcher.Invoke(() => _mainWindow?.ViewModel?.AddItem(item));
            };
            _clipboardMonitor.OnDuplicateUpdated += () =>
            {
                Dispatcher.Invoke(() => _mainWindow?.ViewModel?.LoadItems());
            };
            _clipboardMonitor.Start();

            _cleanupService = new CleanupService(_databaseService);
            if (_settingsService.Settings.EnableAutoCleanup)
            {
                _cleanupService.Start(_settingsService.Settings.RetentionDays);
            }

            _mainWindow.Hide();

            // 检查是否需要执行 Vacuum
            CheckAndPerformPeriodicVacuum();

            // 设置定时器，每天检查一次是否需要执行 Vacuum
            _vacuumTimer = new System.Threading.Timer(
                callback: _ => CheckAndPerformPeriodicVacuum(),
                state: null,
                dueTime: TimeSpan.FromDays(1),
                period: TimeSpan.FromDays(1));

            Current.Exit += (s, args) => CleanupResources();
        }

        private static void CleanupResources()
        {
            _vacuumTimer?.Dispose();
            _clipboardMonitor?.Stop();
            _clipboardMonitor?.Dispose();
            _hotkeyService?.Dispose();
            _cleanupService?.Dispose();
            _trayService?.Dispose();
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

        public static void UpdateHotkey(string hotkey)
        {
            _hotkeyService?.UnregisterAll();
            _hotkeyService?.RegisterHotkey(hotkey, () => 
            {
                Current?.Dispatcher.Invoke(() => _mainWindow?.ToggleVisibility());
            });
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

        public static void ToggleMainWindow()
        {
            _mainWindow?.ToggleVisibility();
        }

        public static void RecreateTrayIcon()
        {
            // 释放旧的托盘图标
            _trayService?.Dispose();

            // 重新创建托盘图标
            if (_settingsService != null && _mainWindow != null)
            {
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
                _trayService = new TrayService(_settingsService, windowHandle);
                _trayService.SetMainWindow(_mainWindow);
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
                    
                    System.Threading.Tasks.Task.Run(() =>
                    {
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
                    });
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
