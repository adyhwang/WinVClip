using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinVClip.Services
{
    public enum PasteShortcutMode
    {
        CtrlV,
        ShiftInsert,
        Auto
    }

    public static class KeyboardService
    {
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_SHIFT = 0x10;
        private const int VK_V = 0x56;
        private const int VK_INSERT = 0x2D;

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;

        private static readonly string[] TerminalProcesses = new[]
        {
            "cmd", "cmd.exe",
            "powershell", "powershell.exe",
            "pwsh", "pwsh.exe",
            "WindowsTerminal", "WindowsTerminal.exe",
            "conhost", "conhost.exe",
            "Terminal", "Terminal.exe",
            "Alacritty", "alacritty.exe",
            "Hyper", "Hyper.exe",
            "FluentTerminal", "FluentTerminal.exe",
            "Console", "Console.exe",
            "ConsoleZ", "ConsoleZ.exe",
            "ConEmu", "ConEmu.exe",
            "ConEmu64", "ConEmu64.exe",
            "Cmder", "Cmder.exe"
        };

        private static bool IsKeyPressed(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        internal static void SendKey(int vk, bool up)
        {
            keybd_event((byte)vk, 0, up ? KEYEVENTF_KEYUP : 0, 0);
        }

        public static void ReleaseModifierKeys()
        {
            bool ctrl = IsKeyPressed(VK_CONTROL);
            bool alt = IsKeyPressed(VK_MENU);
            bool shift = IsKeyPressed(VK_SHIFT);

            if (alt) SendKey(VK_MENU, true);
            if (ctrl) SendKey(VK_CONTROL, true);
            if (shift) SendKey(VK_SHIFT, true);
        }

        public static (bool Ctrl, bool Alt, bool Shift, bool Win) GetModifierKeysState()
        {
            bool ctrl = IsKeyPressed(VK_CONTROL);
            bool alt = IsKeyPressed(VK_MENU);
            bool shift = IsKeyPressed(VK_SHIFT);
            bool win = IsKeyPressed(0x5B) || IsKeyPressed(0x5C);
            return (ctrl, alt, shift, win);
        }

        public static void SimulatePaste(PasteShortcutMode mode = PasteShortcutMode.CtrlV)
        {
            if (mode == PasteShortcutMode.Auto)
            {
                mode = DetermineBestPasteMode();
            }

            if (mode == PasteShortcutMode.ShiftInsert)
            {
                SimulatePasteShiftInsert();
            }
            else
            {
                SimulatePasteCtrlV();
            }
        }

        public static PasteShortcutMode DetermineBestPasteMode()
        {
            var (ctrl, alt, _, _) = GetModifierKeysState();

            if (ctrl || alt)
            {
                return PasteShortcutMode.ShiftInsert;
            }

            var focusService = App.GetFocusService();
            var appInfo = focusService?.GetForegroundAppInfo();

            if (appInfo != null && IsTerminalApp(appInfo.ProcessName))
            {
                return PasteShortcutMode.ShiftInsert;
            }

            return PasteShortcutMode.CtrlV;
        }

        public static bool IsTerminalApp(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            return TerminalProcesses.Any(t =>
                processName.Equals(t, StringComparison.OrdinalIgnoreCase));
        }

        private static void SimulatePasteCtrlV()
        {
            bool userAlt = IsKeyPressed(VK_MENU);

            if (userAlt)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (!IsKeyPressed(VK_MENU)) break;
                    SendKey(VK_MENU, true);
                    Thread.Sleep(5);
                }
            }

            bool userCtrl = IsKeyPressed(VK_CONTROL);

            if (!userCtrl)
            {
                SendKey(VK_CONTROL, false);
            }

            try
            {
                SendKey(VK_V, false);
                Thread.Sleep(8);
                SendKey(VK_V, true);
            }
            finally
            {
                if (!userCtrl)
                {
                    SendKey(VK_CONTROL, true);
                }

                if (userAlt)
                {
                    SendKey(VK_MENU, false);
                    SendKey(VK_CONTROL, false);
                    Thread.Sleep(10);
                    SendKey(VK_CONTROL, true);
                }
            }
        }

        private static void SimulatePasteShiftInsert()
        {
            bool userShift = IsKeyPressed(VK_SHIFT);

            if (!userShift)
            {
                SendKey(VK_SHIFT, false);
            }

            try
            {
                keybd_event(VK_INSERT, 0, KEYEVENTF_EXTENDEDKEY, 0);
                Thread.Sleep(8);
                keybd_event(VK_INSERT, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
            }
            finally
            {
                if (!userShift)
                {
                    SendKey(VK_SHIFT, true);
                }
            }
        }

        public static void SimulateKeyPress(int vk, int delayMs = 8)
        {
            SendKey(vk, false);
            Thread.Sleep(delayMs);
            SendKey(vk, true);
        }

        public static void SimulateKeyCombination(int modifierVk, int keyVk, int delayMs = 8)
        {
            bool modifierPressed = IsKeyPressed(modifierVk);

            if (!modifierPressed)
            {
                SendKey(modifierVk, false);
            }

            try
            {
                SendKey(keyVk, false);
                Thread.Sleep(delayMs);
                SendKey(keyVk, true);
            }
            finally
            {
                if (!modifierPressed)
                {
                    SendKey(modifierVk, true);
                }
            }
        }

        public static void SimulateCopy()
        {
            SimulateKeyCombination(VK_CONTROL, 0x43);
        }

        public static void SimulateCut()
        {
            SimulateKeyCombination(VK_CONTROL, 0x58);
        }

        public static void SimulateSelectAll()
        {
            SimulateKeyCombination(VK_CONTROL, 0x41);
        }

        public static void SimulateEnter()
        {
            SimulateKeyPress(0x0D);
        }

        public static void SimulateEscape()
        {
            SimulateKeyPress(0x1B);
        }

        public static void SimulateTab()
        {
            SimulateKeyPress(0x09);
        }

        public static void SimulateBackspace()
        {
            SimulateKeyPress(0x08);
        }

        public static void SimulateDelete()
        {
            SimulateKeyPress(0x2E);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    }

    public class KeyGuard : IDisposable
    {
        private readonly int _vk;
        private readonly bool _shouldRelease;
        private bool _disposed;

        public KeyGuard(int vk, bool press)
        {
            _vk = vk;
            _shouldRelease = press;

            if (press)
            {
                KeyboardService.SendKey(vk, false);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_shouldRelease)
                {
                    KeyboardService.SendKey(_vk, true);
                }
                _disposed = true;
            }
        }
    }
}
