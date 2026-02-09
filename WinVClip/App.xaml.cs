using System;
using System.IO;
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

        public static DatabaseService DatabaseService => _databaseService ??= new DatabaseService(GetDatabasePath());
        public static SettingsService SettingsService => _settingsService ??= new SettingsService();

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

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "WinVClip_Mutex", out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            _settingsService = SettingsService;
            _databaseService = DatabaseService;

            ThemeService.Instance.Initialize(_settingsService.Settings);

            _mainWindow = new MainWindow(_databaseService, _settingsService);
            _mainWindow.Show();
            
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
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
                // 快捷键注册失败，显示托盘通知
                _trayService.ShowNotification("快捷键注册失败", "当前快捷键可能被占用，请在设置中修改其他快捷键");
            }
            else
            {
                // 快捷键注册成功，显示托盘通知
                _trayService.ShowNotification("WinVClip 已启动", $"快捷键 {_settingsService.Settings.Hotkey} 已注册\n单击托盘图标显示主界面");
            }

            _clipboardMonitor = new ClipboardMonitor(_databaseService, _settingsService);
            _clipboardMonitor.OnClipboardChanged += item => 
            {
                Dispatcher.Invoke(() => _mainWindow?.ViewModel?.AddItem(item));
            };
            _clipboardMonitor.Start();

            _cleanupService = new CleanupService(_databaseService);
            if (_settingsService.Settings.EnableAutoCleanup)
            {
                _cleanupService.Start(_settingsService.Settings.RetentionDays);
            }

            _mainWindow.Hide();

            Current.Exit += (s, args) =>
            {
                _clipboardMonitor?.Stop();
                _clipboardMonitor?.Dispose();
                _hotkeyService?.Dispose();
                _cleanupService?.Dispose();
                _trayService?.Dispose();
                _databaseService?.Dispose();
                _mutex?.Dispose();
            };
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

        protected override void OnExit(ExitEventArgs e)
        {
            _clipboardMonitor?.Stop();
            _clipboardMonitor?.Dispose();
            _hotkeyService?.Dispose();
            _trayService?.Dispose();
            _databaseService?.Dispose();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
