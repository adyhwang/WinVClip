using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using WinVClip.Models;

namespace WinVClip.Services
{
    public class ThemeService : IDisposable
    {
        private static ThemeService? _instance;
        private AppSettings? _settings;
        private RegistryMonitor? _registryMonitor;
        private string? _currentAppliedTheme;

        public static ThemeService Instance => _instance ??= new ThemeService();

        public event EventHandler<string>? ThemeChanged;

        private ThemeService()
        {
        }

        public void Initialize(AppSettings settings)
        {
            _settings = settings;
            ApplyTheme(_settings.Theme);

            if (_settings.Theme == "Auto")
            {
                StartSystemThemeMonitoring();
            }
        }

        private void StartSystemThemeMonitoring()
        {
            StopSystemThemeMonitoring();

            _registryMonitor = new RegistryMonitor(
                RegistryHive.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            _registryMonitor.RegChanged += OnSystemThemeChanged;
            _registryMonitor.Start();
        }

        private void StopSystemThemeMonitoring()
        {
            if (_registryMonitor != null)
            {
                _registryMonitor.RegChanged -= OnSystemThemeChanged;
                _registryMonitor.Stop();
                _registryMonitor.Dispose();
                _registryMonitor = null;
            }
        }

        private void OnSystemThemeChanged(object? sender, EventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_settings?.Theme == "Auto")
                {
                    ApplyTheme("Auto");
                }
            });
        }

        public void ApplyTheme(string theme)
        {
            var app = Application.Current;
            if (app == null) return;

            var resourceDictionary = new ResourceDictionary();
            string actualTheme;

            switch (theme)
            {
                case "Dark":
                    resourceDictionary.Source = new Uri("/WinVClip;component/Themes/DarkTheme.xaml", UriKind.Relative);
                    actualTheme = "Dark";
                    break;
                case "Light":
                    resourceDictionary.Source = new Uri("/WinVClip;component/Themes/LightTheme.xaml", UriKind.Relative);
                    actualTheme = "Light";
                    break;
                case "Auto":
                default:
                    if (ShouldUseDarkTheme())
                    {
                        resourceDictionary.Source = new Uri("/WinVClip;component/Themes/DarkTheme.xaml", UriKind.Relative);
                        actualTheme = "Dark";
                    }
                    else
                    {
                        resourceDictionary.Source = new Uri("/WinVClip;component/Themes/LightTheme.xaml", UriKind.Relative);
                        actualTheme = "Light";
                    }
                    break;
            }

            // 先添加新资源，再移除旧资源，避免资源查找失败
            var oldDictionaries = app.Resources.MergedDictionaries.ToList();
            app.Resources.MergedDictionaries.Add(resourceDictionary);
            foreach (var oldDict in oldDictionaries)
            {
                app.Resources.MergedDictionaries.Remove(oldDict);
            }

            if (_currentAppliedTheme != actualTheme)
            {
                _currentAppliedTheme = actualTheme;
                ThemeChanged?.Invoke(this, actualTheme);
            }
        }

        private bool ShouldUseDarkTheme()
        {
            try
            {
                var appsUseLightTheme = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")?.GetValue("AppsUseLightTheme");
                return appsUseLightTheme != null && appsUseLightTheme.ToString() == "0";
            }
            catch
            {
                return false;
            }
        }

        public void UpdateTheme(string theme)
        {
            if (_settings != null)
            {
                _settings.Theme = theme;

                if (theme == "Auto")
                {
                    StartSystemThemeMonitoring();
                }
                else
                {
                    StopSystemThemeMonitoring();
                }

                ApplyTheme(theme);
            }
        }

        public void Dispose()
        {
            StopSystemThemeMonitoring();
        }
    }

    public class RegistryMonitor : IDisposable
    {
        private readonly RegistryHive _hive;
        private readonly string _registryPath;
        private IntPtr _registryKey;
        private IntPtr _eventHandle;
        private bool _isMonitoring;
        private readonly object _lockObject = new object();

        public event EventHandler? RegChanged;

        public RegistryMonitor(RegistryHive hive, string registryPath)
        {
            _hive = hive;
            _registryPath = registryPath;
        }

        public void Start()
        {
            lock (_lockObject)
            {
                if (_isMonitoring) return;

                uint result = RegOpenKeyEx(
                    (IntPtr)_hive,
                    _registryPath,
                    0,
                    KEY_NOTIFY | KEY_READ,
                    out _registryKey);

                if (result != 0)
                    throw new Exception($"Failed to open registry key. Error code: {result}");

                _eventHandle = CreateEvent(IntPtr.Zero, false, false, null);
                if (_eventHandle == IntPtr.Zero)
                    throw new Exception("Failed to create event handle.");

                _isMonitoring = true;
                ThreadPool.QueueUserWorkItem(MonitorLoop);
            }
        }

        public void Stop()
        {
            lock (_lockObject)
            {
                _isMonitoring = false;

                if (_eventHandle != IntPtr.Zero)
                {
                    SetEvent(_eventHandle);
                }
            }
        }

        private void MonitorLoop(object? state)
        {
            while (_isMonitoring)
            {
                uint result = RegNotifyChangeKeyValue(
                    _registryKey,
                    false,
                    REG_NOTIFY_CHANGE_LAST_SET,
                    _eventHandle,
                    true);

                if (result != 0)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                WaitForSingleObject(_eventHandle, INFINITE);

                if (_isMonitoring)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        RegChanged?.Invoke(this, EventArgs.Empty);
                    });
                }
            }
        }

        public void Dispose()
        {
            Stop();

            if (_eventHandle != IntPtr.Zero)
            {
                CloseHandle(_eventHandle);
                _eventHandle = IntPtr.Zero;
            }

            if (_registryKey != IntPtr.Zero)
            {
                RegCloseKey(_registryKey);
                _registryKey = IntPtr.Zero;
            }
        }

        private const uint KEY_NOTIFY = 0x0010;
        private const uint KEY_READ = 0x20019;
        private const uint REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
        private const uint INFINITE = 0xFFFFFFFF;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll")]
        private static extern uint RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, uint dwNotifyFilter, IntPtr hEvent, bool fAsynchronous);

        [DllImport("advapi32.dll")]
        private static extern uint RegCloseKey(IntPtr hKey);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    }
}