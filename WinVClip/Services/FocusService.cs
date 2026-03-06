using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WinVClip.Services
{
    public class FocusService : IDisposable
    {
        private IntPtr _winEventHook;
        private IntPtr _lastFocusHwnd;
        private readonly HashSet<IntPtr> _excludedHwnds = new HashSet<IntPtr>();
        private bool _isMonitoring;
        private bool _disposed;
        private readonly object _lock = new object();

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

        private WinEventDelegate _winEventDelegate;

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public IntPtr LastFocusHwnd => _lastFocusHwnd;
        public event Action<IntPtr>? FocusChanged;

        public FocusService()
        {
            _winEventDelegate = WinEventProc;
            _lastFocusHwnd = IntPtr.Zero;
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            lock (_lock)
            {
                if (_isMonitoring) return;

                _winEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0, 0,
                    WINEVENT_OUTOFCONTEXT);

                _isMonitoring = _winEventHook != IntPtr.Zero;
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            lock (_lock)
            {
                if (!_isMonitoring) return;

                if (_winEventHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_winEventHook);
                    _winEventHook = IntPtr.Zero;
                }

                _isMonitoring = false;
            }
        }

        public void AddExcludedHwnd(IntPtr hwnd)
        {
            lock (_excludedHwnds)
            {
                _excludedHwnds.Add(hwnd);
            }
        }

        public void RemoveExcludedHwnd(IntPtr hwnd)
        {
            lock (_excludedHwnds)
            {
                _excludedHwnds.Remove(hwnd);
            }
        }

        public void ClearExcludedHwnds()
        {
            lock (_excludedHwnds)
            {
                _excludedHwnds.Clear();
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero) return;

            lock (_excludedHwnds)
            {
                if (_excludedHwnds.Contains(hwnd)) return;
            }

            if (IsSystemWindow(hwnd)) return;

            _lastFocusHwnd = hwnd;
            FocusChanged?.Invoke(hwnd);
        }

        private bool IsSystemWindow(IntPtr hwnd)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, 256);
            string classNameStr = className.ToString();

            StringBuilder windowText = new StringBuilder(256);
            GetWindowText(hwnd, windowText, 256);
            string windowTextStr = windowText.ToString();

            if (classNameStr == "Shell_TrayWnd" ||
                classNameStr == "Shell_SecondaryTrayWnd" ||
                classNameStr == "NotifyIconOverflowWindow" ||
                classNameStr == "TopLevelWindowForOverflowXamlIsland" ||
                classNameStr.StartsWith("Windows.UI.") ||
                classNameStr == "#32768" ||
                classNameStr == "DropDown" ||
                classNameStr == "Xaml_WindowedPopupClass")
            {
                return true;
            }

            if (windowTextStr == "WinVClip" || windowTextStr == "菜单")
            {
                return true;
            }

            return false;
        }

        public void RestoreLastFocus()
        {
            if (_lastFocusHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(_lastFocusHwnd);
            }
        }

        public ForegroundAppInfo? GetForegroundAppInfo()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return null;

            StringBuilder titleBuf = new StringBuilder(512);
            GetWindowText(hwnd, titleBuf, 512);
            string windowTitle = titleBuf.ToString();

            string processPath = string.Empty;
            string processName = string.Empty;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                StringBuilder pathBuf = new StringBuilder(260);
                if (GetModuleFileNameEx(hProcess, IntPtr.Zero, pathBuf, 260) > 0)
                {
                    processPath = pathBuf.ToString();
                    processName = System.IO.Path.GetFileName(processPath);
                }
                CloseHandle(hProcess);
            }

            if (string.IsNullOrEmpty(processName))
            {
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero)
                {
                    StringBuilder pathBuf = new StringBuilder(260);
                    if (GetModuleFileNameEx(hProcess, IntPtr.Zero, pathBuf, 260) > 0)
                    {
                        processPath = pathBuf.ToString();
                        processName = System.IO.Path.GetFileName(processPath);
                    }
                    CloseHandle(hProcess);
                }
            }

            if (string.IsNullOrEmpty(processName)) return null;

            return new ForegroundAppInfo
            {
                ProcessName = processName,
                ProcessPath = processPath,
                WindowTitle = windowTitle
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();
                _disposed = true;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    }

    public class ForegroundAppInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
    }
}
