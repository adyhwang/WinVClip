using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinVClip.Services;

namespace WinVClip.Services
{
    public class TrayService : IDisposable
    {
        private readonly IntPtr _windowHandle;
        private readonly SettingsService _settingsService;
        private bool _isMonitoringEnabled = true;
        private readonly int _trayIconId;
        private Icon? _icon;
        private bool _disposed;

        // explorer 重启后任务栏重建消息（RegisterWindowMessage("TaskbarCreated")）
        private readonly uint _taskbarCreatedMessage;
        // 防抖定时器：合并短时间内可能多次到达的 TaskbarCreated 广播
        private System.Windows.Threading.DispatcherTimer? _recreateTimer;
        private const int RecreateDebounceMs = 500;

        public bool IsMonitoringEnabled
        {
            get => _isMonitoringEnabled;
            set
            {
                _isMonitoringEnabled = value;
                UpdateTrayIcon();
            }
        }

        public event Action? OnShowWindow;
        public event Action? OnOpenSettings;
        public event Action? OnExit;
        public event Action<bool>? OnMonitoringToggled;

        public TrayService(SettingsService settingsService, IntPtr windowHandle)
        {
            _settingsService = settingsService;
            _windowHandle = windowHandle;
            _trayIconId = new Random().Next();

            _icon = LoadIcon();
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

            AddTrayIcon();
            RegisterTrayMessageHandler();
        }

        private Icon LoadIcon()
        {
            try
            {
                // 从程序集资源加载图标
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                // 根据监控状态动态选择图标
                var iconName = _settingsService.Settings.MonitorEnabled ? "app.ico" : "app_disabled.ico";
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(iconName, StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch
            {
            }

            // 回退到系统默认图标
            return SystemIcons.Application;
        }

        private void AddTrayIcon()
        {
            var data = new NotifyIconData
            {
                cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _windowHandle,
                uID = _trayIconId,
                uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _icon?.Handle ?? IntPtr.Zero
            };
            data.szTip = "WinVClip";

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private void RegisterTrayMessageHandler()
        {
            var source = HwndSource.FromHwnd(_windowHandle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // explorer 重启后任务栏重建，重新创建托盘图标（带防抖）
            if (_taskbarCreatedMessage != 0 && msg == (int)_taskbarCreatedMessage)
            {
                ScheduleRecreateTrayIcon();
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_TRAYICON)
            {
                switch ((int)lParam)
                {
                    case WM_LBUTTONUP:
                        OnShowWindow?.Invoke();
                        handled = true;
                        break;
                    case WM_RBUTTONUP:
                        ShowContextMenu();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void ShowContextMenu()
        {
            // 使用微软官方推荐的模式 (KB 135788)：
            // 在显示托盘菜单前调用 SetForegroundWindow，让隐藏的主窗口获得前台状态，
            // 这样 WPF ContextMenu 才能正确追踪外部点击事件来自动关闭菜单。
            SetForegroundWindow(_windowHandle);
            
            var menu = new System.Windows.Controls.ContextMenu();
            menu.Style = Application.Current.TryFindResource("ContextMenuStyle") as Style;
            
            // 显示主界面
            var showItem = CreateMenuItem(Loc.Get("Tray.Show", "显示主界面"), "📋", Loc.Get("Tray.Show", "显示 WinVClip 主窗口"), () => OnShowWindow?.Invoke());
            menu.Items.Add(showItem);
            
            // 设置
            var settingsItem = CreateMenuItem(Loc.Get("Tray.Settings", "设置"), "⚙️", Loc.Get("Tray.Settings", "打开设置窗口"), () => OnOpenSettings?.Invoke());
            menu.Items.Add(settingsItem);
            
            // 分隔线
            menu.Items.Add(CreateSeparator());
            
            // 监控开关
            var monitoringItem = new System.Windows.Controls.MenuItem 
            { 
                Header = _settingsService.Settings.MonitorEnabled ? Loc.Get("Tray.MonitoringDisabled", "禁用监听") : Loc.Get("Tray.MonitoringEnabled", "启用监听"),
                Icon = _settingsService.Settings.MonitorEnabled ? "⛔︎" : "👁︎",
                ToolTip = Loc.Get("Tray.ToggleMonitoring", "切换剪贴板监控状态")
            };
            monitoringItem.Style = Application.Current.TryFindResource("MenuItemStyle") as Style;
            monitoringItem.Click += (s, e) => ToggleMonitoring();
            menu.Items.Add(monitoringItem);
            
            // 分隔线
            menu.Items.Add(CreateSeparator());
            
            // 退出
            var exitItem = CreateMenuItem(Loc.Get("Tray.Exit", "退出"), "❌", Loc.Get("Tray.Exit", "退出 WinVClip"), () => OnExit?.Invoke());
            menu.Items.Add(exitItem);
            
            // 在鼠标位置显示菜单
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.PlacementTarget = Application.Current.MainWindow;
            menu.IsOpen = true;
            
            // 菜单显示后发送 WM_NULL，确保窗口消息队列继续正常处理
            PostMessage(_windowHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        }

        private System.Windows.Controls.MenuItem CreateMenuItem(string header, string icon, string tooltip, Action onClick)
        {
            var item = new System.Windows.Controls.MenuItem 
            { 
                Header = header,
                Icon = icon,
                ToolTip = tooltip
            };
            item.Style = Application.Current.TryFindResource("MenuItemStyle") as Style;
            item.Click += (s, e) => onClick();
            return item;
        }

        private System.Windows.Controls.Separator CreateSeparator()
        {
            var separator = new System.Windows.Controls.Separator();
            separator.Style = Application.Current.TryFindResource("SeparatorStyle") as Style;
            return separator;
        }

        private void ToggleMonitoring()
        {
            _settingsService.Settings.MonitorEnabled = !_settingsService.Settings.MonitorEnabled;
            _settingsService.SaveSettings();
            UpdateTrayIcon(); // 更新托盘图标
            OnMonitoringToggled?.Invoke(_settingsService.Settings.MonitorEnabled);
        }

        private void UpdateTrayIcon()
        {
            _icon = LoadIcon();
            var data = new NotifyIconData
            {
                cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _windowHandle,
                uID = _trayIconId,
                uFlags = NIF_ICON,
                hIcon = _icon?.Handle ?? IntPtr.Zero
            };

            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        /// <summary>
        /// 防抖调度：收到 TaskbarCreated 消息后延迟重建托盘图标，
        /// 合并 explorer 重启过程中可能多次广播的消息，避免重复创建。
        /// </summary>
        private void ScheduleRecreateTrayIcon()
        {
            if (_disposed) return;

            if (_recreateTimer == null)
            {
                _recreateTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(RecreateDebounceMs)
                };
                _recreateTimer.Tick += (s, e) =>
                {
                    _recreateTimer!.Stop();
                    RecreateTrayIcon();
                };
            }

            // 每次收到消息都重置定时器，只在最后一次消息后延迟触发重建
            _recreateTimer.Stop();
            _recreateTimer.Start();
        }

        /// <summary>
        /// 重建托盘图标：先删除可能残留的旧图标，再重新添加。
        /// 在 explorer 重启、任务栏重建后调用。
        /// </summary>
        private void RecreateTrayIcon()
        {
            if (_disposed) return;

            try
            {
                // 先尝试删除可能残留的旧图标（explorer 已销毁时会失败，忽略即可）
                var deleteData = new NotifyIconData
                {
                    cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                    hWnd = _windowHandle,
                    uID = _trayIconId
                };
                Shell_NotifyIcon(NIM_DELETE, ref deleteData);

                // 重新添加托盘图标，复用当前 _icon（仍由本进程持有，句柄有效）
                AddTrayIcon();
            }
            catch
            {
                // 重建过程中发生异常时忽略，避免影响主窗口
            }
        }

        public void ShowNotification(string title, string message)
        {
            var data = new NotifyIconData
            {
                cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _windowHandle,
                uID = _trayIconId,
                uFlags = NIF_INFO,
                dwInfoFlags = NIIF_INFO,
                szInfoTitle = title + "\0",
                szInfo = message + "\0",
                uTimeout = 3000
            };

            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _recreateTimer?.Stop();

                var data = new NotifyIconData
                {
                    cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                    hWnd = _windowHandle,
                    uID = _trayIconId
                };

                Shell_NotifyIcon(NIM_DELETE, ref data);
                _icon?.Dispose();
                _disposed = true;
            }
        }

        #region Windows API

        private const int WM_TRAYICON = 0x0400;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_NULL = 0x0000;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;
        private const int NIIF_INFO = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData lpData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        #endregion
    }
}
