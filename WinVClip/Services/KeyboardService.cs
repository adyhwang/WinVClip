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

        private static void ReleaseKey(int vk)
        {
            for (int i = 0; i < 20; i++)
            {
                if (!IsKeyPressed(vk)) break;
                SendKey(vk, true);
                Thread.Sleep(5);
            }
        }

        public static (bool Ctrl, bool Alt, bool Shift, bool Win) GetModifierKeysState()
        {
            bool ctrl = IsKeyPressed(VK_CONTROL);
            bool alt = IsKeyPressed(VK_MENU);
            bool shift = IsKeyPressed(VK_SHIFT);
            bool win = IsKeyPressed(0x5B) || IsKeyPressed(0x5C);
            return (ctrl, alt, shift, win);
        }

        public static bool WaitForModifiersReleased(int maxWaitMs)
        {
            int waited = 0;
            while (waited < maxWaitMs)
            {
                var (ctrl, alt, shift, win) = GetModifierKeysState();
                if (!ctrl && !alt && !shift && !win)
                    return true;
                Thread.Sleep(10);
                waited += 10;
            }

            var (c, a, s, w) = GetModifierKeysState();
            return !c && !a && !s && !w;
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

        #region SendInput

        private const uint INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private static INPUT KeyboardInput(int vk, bool up, uint extraFlags = 0)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        dwFlags = (up ? KEYEVENTF_KEYUP : 0u) | extraFlags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private static bool SendKeyInputs(params INPUT[] inputs)
        {
            if (inputs == null || inputs.Length == 0) return false;
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
        }

        #endregion

        private static void SimulatePasteCtrlV()
        {
            bool userAlt = IsKeyPressed(VK_MENU);
            bool userShift = IsKeyPressed(VK_SHIFT);

            if (userAlt) ReleaseKey(VK_MENU);
            if (userShift) ReleaseKey(VK_SHIFT);

            bool userCtrl = IsKeyPressed(VK_CONTROL);

            try
            {
                if (userCtrl)
                {
                    SendKeyInputs(KeyboardInput(VK_V, false));
                    Thread.Sleep(8);
                    SendKeyInputs(KeyboardInput(VK_V, true));
                }
                else
                {
                    SendKeyInputs(KeyboardInput(VK_CONTROL, false), KeyboardInput(VK_V, false));
                    Thread.Sleep(8);
                    SendKeyInputs(KeyboardInput(VK_V, true), KeyboardInput(VK_CONTROL, true));
                }
            }
            finally
            {
                if (userShift)
                {
                    SendKey(VK_SHIFT, false);
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
            bool userCtrl = IsKeyPressed(VK_CONTROL);
            bool userAlt = IsKeyPressed(VK_MENU);

            if (userCtrl) ReleaseKey(VK_CONTROL);
            if (userAlt) ReleaseKey(VK_MENU);

            bool userShift = IsKeyPressed(VK_SHIFT);

            try
            {
                if (userShift)
                {
                    SendKeyInputs(KeyboardInput(VK_INSERT, false, KEYEVENTF_EXTENDEDKEY));
                    Thread.Sleep(8);
                    SendKeyInputs(KeyboardInput(VK_INSERT, true, KEYEVENTF_EXTENDEDKEY));
                }
                else
                {
                    SendKeyInputs(KeyboardInput(VK_SHIFT, false), KeyboardInput(VK_INSERT, false, KEYEVENTF_EXTENDEDKEY));
                    Thread.Sleep(8);
                    SendKeyInputs(KeyboardInput(VK_INSERT, true, KEYEVENTF_EXTENDEDKEY), KeyboardInput(VK_SHIFT, true));
                }
            }
            finally
            {
                if (userAlt)
                {
                    SendKey(VK_MENU, false);
                }

                if (userCtrl)
                {
                    SendKey(VK_CONTROL, false);
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

        private const int VK_LWIN = 0x5B;
        private const int VK_OEM_PERIOD = 0xBE;

        public static void SendWinPeriod()
        {
            SendKey(VK_LWIN, false);
            Thread.Sleep(5);
            SendKey(VK_OEM_PERIOD, false);
            Thread.Sleep(5);
            SendKey(VK_OEM_PERIOD, true);
            Thread.Sleep(5);
            SendKey(VK_LWIN, true);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    }
}
