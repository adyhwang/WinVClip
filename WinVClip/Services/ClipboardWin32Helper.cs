using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinVClip.Services
{
    
    
    
    
    
    
    public static class ClipboardWin32Helper
    {
        
        
        

        private const int MaxRetries = 5;
        private const int RetryDelayMs = 50;

        private const uint CF_UNICODETEXT = 13;
        private const uint CF_TEXT = 1;
        private const uint GMEM_MOVEABLE = 0x0002;

        
        
        

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

        
        
        

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        
        
        

        
        private static readonly object _syncLock = new object();

        
        private static IntPtr _hwnd = IntPtr.Zero;

        
        
        

        private static readonly object _loadingLock = new object();
        private static Thread _loadingThread;
        private static WinVClip.LoadingTipWindow _loadingWindow;
        private static volatile bool _closeRequested;

        
        
        

        
        public static void SetWindowHandle(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        
        
        

        
        
        
        
        public static void ShowClipboardLoading()
        {
            lock (_loadingLock)
            {
                if (_loadingWindow != null || _loadingThread != null)
                    return; 

                _closeRequested = false;
                var thread = new Thread(() =>
                {
                    var window = new WinVClip.LoadingTipWindow();

                    lock (_loadingLock)
                    {
                        _loadingWindow = window;
                    }

                    
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

        
        public static void Shutdown()
        {
            CloseClipboardLoading();
        }

        
        
        

        
        
        public static void SetText(string text)
        {
            if (text == null) text = "";

            lock (_syncLock)
            {
                bool loadingShown = false;
                try
                {
                    
                    for (int i = 0; i < MaxRetries; i++)
                    {
                        if (TrySetTextNative(text))
                            return;

                        
                        if (!loadingShown)
                        {
                            ShowClipboardLoading();
                            loadingShown = true;
                        }

                        Thread.Sleep(RetryDelayMs);
                    }

                    
                    WpfRetry(() => System.Windows.Clipboard.SetText(text));
                }
                finally
                {
                    if (loadingShown)
                        CloseClipboardLoading();
                }
            }
        }

        
        public static string GetText()
        {
            lock (_syncLock)
            {
                
                for (int i = 0; i < MaxRetries; i++)
                {
                    string result = TryGetTextNative();
                    if (result != null)
                        return result;
                    Thread.Sleep(RetryDelayMs);
                }

                
                return WpfRetry(() => System.Windows.Clipboard.GetText());
            }
        }

        
        public static bool ContainsText()
        {
            lock (_syncLock)
            {
                return IsClipboardFormatAvailable(CF_UNICODETEXT)
                    || IsClipboardFormatAvailable(CF_TEXT);
            }
        }

        
        
        

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
                
                WpfRetry(() => System.Windows.Clipboard.Clear());
            }
        }

        
        
        

        
        private static bool TrySetTextNative(string text)
        {
            if (!OpenClipboard(_hwnd))
                return false;

            try
            {
                if (!EmptyClipboard())
                    return false;

                int byteCount = (text.Length + 1) * 2; 
                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                    return false;
                }

                
                if (text.Length > 0)
                    Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
                Marshal.WriteInt16(pGlobal + text.Length * 2, 0);

                GlobalUnlock(hGlobal);

                
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
            action(); 
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
            return func(); 
        }
    }
}
