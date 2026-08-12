using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinVClip.Services
{
    /// <summary>
    /// 基于 Win32 原生 API 的剪贴板辅助类，参考 Ditto/CopyQ 实现。
    /// 提供带重试的安全读写，全局锁防止并发调用。
    /// 文本操作使用原生 user32 API（CF_UNICODETEXT），
    /// 复杂类型（图片/文件/DataObject）使用 WPF Clipboard 加锁+重试封装。
    /// </summary>
    public static class ClipboardWin32Helper
    {
        // ═══════════════════════════════════════════
        //  常量
        // ═══════════════════════════════════════════

        private const int MaxRetries = 5;
        private const int RetryDelayMs = 50;

        private const uint CF_UNICODETEXT = 13;
        private const uint CF_TEXT = 1;
        private const uint GMEM_MOVEABLE = 0x0002;

        // ═══════════════════════════════════════════
        //  P/Invoke — user32.dll
        // ═══════════════════════════════════════════

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint uFormat);

        // ═══════════════════════════════════════════
        //  P/Invoke — kernel32.dll
        // ═══════════════════════════════════════════

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        // ═══════════════════════════════════════════
        //  字段
        // ═══════════════════════════════════════════

        /// <summary>全局同步锁，防止并发访问剪贴板。</summary>
        private static readonly object _syncLock = new object();

        /// <summary>主窗口真实 HWND，供 OpenClipboard 使用。</summary>
        private static IntPtr _hwnd = IntPtr.Zero;

        // ═══════════════════════════════════════════
        //  Loading 弹窗 STA 线程管理
        // ═══════════════════════════════════════════

        private static readonly object _loadingLock = new object();
        private static Thread _loadingThread;
        private static WinVClip.LoadingTipWindow _loadingWindow;
        private static volatile bool _closeRequested;

        // ═══════════════════════════════════════════
        //  初始化
        // ═══════════════════════════════════════════

        /// <summary>设置主窗口句柄，供 OpenClipboard 使用。应在 SourceInitialized 中调用。</summary>
        public static void SetWindowHandle(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        // ═══════════════════════════════════════════
        //  Loading 弹窗 — 独立 STA 线程
        // ═══════════════════════════════════════════

        /// <summary>
        /// 在独立 STA 线程上创建并显示 Loading 弹窗。
        /// 避免主 UI 线程被 OpenClipboard 阻塞时 Loading 界面卡死。
        /// </summary>
        public static void ShowClipboardLoading()
        {
            lock (_loadingLock)
            {
                if (_loadingWindow != null || _loadingThread != null)
                    return; // 已在显示

                _closeRequested = false;
                var thread = new Thread(() =>
                {
                    var window = new WinVClip.LoadingTipWindow();

                    lock (_loadingLock)
                    {
                        _loadingWindow = window;
                    }

                    // 窗口创建期间可能已收到关闭请求
                    if (_closeRequested)
                    {
                        lock (_loadingLock)
                        {
                            _loadingWindow = null;
                            _loadingThread = null;
                        }
                        return;
                    }

                    window.Closed += (s, e) =>
                    {
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    };

                    window.Show();
                    Dispatcher.Run();

                    // Dispatcher 退出后清理
                    lock (_loadingLock)
                    {
                        _loadingWindow = null;
                        _loadingThread = null;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                _loadingThread = thread;
                thread.Start();
            }
        }

        /// <summary>关闭 Loading 弹窗。若窗口尚未创建则标记关闭请求，由 STA 线程自行退出。</summary>
        public static void CloseClipboardLoading()
        {
            WinVClip.LoadingTipWindow window;
            lock (_loadingLock)
            {
                _closeRequested = true;
                window = _loadingWindow;
            }

            if (window != null)
            {
                try
                {
                    window.Dispatcher.BeginInvoke(new Action(() => window.Close()));
                }
                catch { }
            }
        }

        /// <summary>应用退出时安全关闭 STA 线程，防止内存泄漏。应在 App.CleanupResources 中调用。</summary>
        public static void Shutdown()
        {
            CloseClipboardLoading();
        }

        // ═══════════════════════════════════════════
        //  文本 — 原生 Win32 API
        // ═══════════════════════════════════════════

        /// <summary>安全设置剪贴板文本（Unicode），带重试。先尝试原生 API，失败回退 WPF。
        /// 首次失败后显示独立 STA 线程 Loading 弹窗，全部失败则异常向上传播由调用方弹 MessageBox。</summary>
        public static void SetText(string text)
        {
            if (text == null) text = "";

            lock (_syncLock)
            {
                bool loadingShown = false;
                try
                {
                    // 原生 Win32 API
                    for (int i = 0; i < MaxRetries; i++)
                    {
                        if (TrySetTextNative(text))
                            return;

                        // 首次失败后显示 Loading（后续重试期间用户可见）
                        if (!loadingShown)
                        {
                            ShowClipboardLoading();
                            loadingShown = true;
                        }

                        Thread.Sleep(RetryDelayMs);
                    }

                    // 回退 WPF
                    WpfRetry(() => System.Windows.Clipboard.SetText(text));
                }
                finally
                {
                    if (loadingShown)
                        CloseClipboardLoading();
                }
            }
        }

        /// <summary>安全读取剪贴板文本（Unicode），带重试。先尝试原生 API，失败回退 WPF。</summary>
        public static string GetText()
        {
            lock (_syncLock)
            {
                // 原生 Win32 API
                for (int i = 0; i < MaxRetries; i++)
                {
                    string result = TryGetTextNative();
                    if (result != null)
                        return result;
                    Thread.Sleep(RetryDelayMs);
                }

                // 回退 WPF
                return WpfRetry(() => System.Windows.Clipboard.GetText());
            }
        }

        /// <summary>检查剪贴板是否包含文本格式。</summary>
        public static bool ContainsText()
        {
            lock (_syncLock)
            {
                return IsClipboardFormatAvailable(CF_UNICODETEXT)
                    || IsClipboardFormatAvailable(CF_TEXT);
            }
        }

        // ═══════════════════════════════════════════
        //  图片 — WPF 封装 + 加锁 + 重试
        // ═══════════════════════════════════════════

        public static void SetImage(BitmapSource image)
        {
            lock (_syncLock)
            {
                WpfRetry(() => System.Windows.Clipboard.SetImage(image));
            }
        }

        public static BitmapSource GetImage()
        {
            lock (_syncLock)
            {
                return WpfRetry(() => System.Windows.Clipboard.GetImage());
            }
        }

        public static bool ContainsImage()
        {
            lock (_syncLock)
            {
                return WpfRetry(() => System.Windows.Clipboard.ContainsImage());
            }
        }

        // ═══════════════════════════════════════════
        //  文件拖放列表 — WPF 封装 + 加锁 + 重试
        // ═══════════════════════════════════════════

        public static void SetFileDropList(StringCollection fileDropList)
        {
            lock (_syncLock)
            {
                WpfRetry(() => System.Windows.Clipboard.SetFileDropList(fileDropList));
            }
        }

        public static bool ContainsFileDropList()
        {
            lock (_syncLock)
            {
                return WpfRetry(() => System.Windows.Clipboard.ContainsFileDropList());
            }
        }

        // ═══════════════════════════════════════════
        //  DataObject — WPF 封装 + 加锁 + 重试
        // ═══════════════════════════════════════════

        public static void SetDataObject(IDataObject data)
        {
            lock (_syncLock)
            {
                WpfRetry(() => System.Windows.Clipboard.SetDataObject(data));
            }
        }

        public static IDataObject GetDataObject()
        {
            lock (_syncLock)
            {
                return WpfRetry(() => System.Windows.Clipboard.GetDataObject());
            }
        }

        // ═══════════════════════════════════════════
        //  清空
        // ═══════════════════════════════════════════

        public static void Clear()
        {
            lock (_syncLock)
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    if (OpenClipboard(_hwnd))
                    {
                        try
                        {
                            EmptyClipboard();
                            return;
                        }
                        finally
                        {
                            CloseClipboard();
                        }
                    }
                    Thread.Sleep(RetryDelayMs);
                }
                // 回退 WPF
                WpfRetry(() => System.Windows.Clipboard.Clear());
            }
        }

        // ═══════════════════════════════════════════
        //  原生 Win32 实现
        // ═══════════════════════════════════════════

        /// <summary>原生设置文本。成功返回 true，OpenClipboard 失败返回 false 触发重试。</summary>
        private static bool TrySetTextNative(string text)
        {
            if (!OpenClipboard(_hwnd))
                return false;

            try
            {
                if (!EmptyClipboard())
                    return false;

                int byteCount = (text.Length + 1) * 2; // Unicode + null 终止符
                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    return false;
                }

                // 写入字符串 + null 终止符
                if (text.Length > 0)
                    Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
                Marshal.WriteInt16(pGlobal + text.Length * 2, 0);

                GlobalUnlock(hGlobal);

                // SetClipboardData 成功后内存所有权转移给剪贴板，不可 GlobalFree
                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    return false;
                }

                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }

        /// <summary>原生读取文本。成功返回字符串，无文本返回 ""，OpenClipboard 失败返回 null 触发重试。</summary>
        private static string TryGetTextNative()
        {
            if (!OpenClipboard(_hwnd))
                return null;

            try
            {
                if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                    return "";

                IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                if (hData == IntPtr.Zero)
                    return "";

                IntPtr pData = GlobalLock(hData);
                if (pData == IntPtr.Zero)
                    return "";

                try
                {
                    return Marshal.PtrToStringUni(pData) ?? "";
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        // ═══════════════════════════════════════════
        //  WPF 重试辅助
        // ═══════════════════════════════════════════

        private static void WpfRetry(Action action)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception)
                {
                    if (i < MaxRetries - 1)
                        Thread.Sleep(RetryDelayMs);
                }
            }
            action(); // 最后一次不捕获，让异常向上传播
        }

        private static T WpfRetry<T>(Func<T> func)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    return func();
                }
                catch (Exception)
                {
                    if (i < MaxRetries - 1)
                        Thread.Sleep(RetryDelayMs);
                }
            }
            return func(); // 最后一次不捕获
        }
    }
}
