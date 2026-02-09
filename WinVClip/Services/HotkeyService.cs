using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WinVClip.Services
{
    public class HotkeyService : IDisposable
    {
        private readonly IntPtr _windowHandle;
        private readonly Dictionary<int, Action> _hotkeyActions = new Dictionary<int, Action>();
        private readonly Dictionary<string, int> _hotkeyToId = new Dictionary<string, int>();
        private int _nextId = 1;
        private bool _disposed;

        public HotkeyService(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public bool RegisterHotkey(string hotkey, Action action)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hotkey) || action == null)
                    return false;

                var modifiers = ParseModifiers(hotkey);
                var key = ParseKey(hotkey);

                if (key == Key.None)
                    return false;

                // 如果没有修饰键，必须是 F1-F12 功能键
                if (modifiers == 0 && !IsFunctionKey(key.ToString()))
                    return false;

                // 标准化快捷键字符串
                var normalizedHotkey = NormalizeHotkey(hotkey);

                // 检查是否已注册
                if (_hotkeyToId.TryGetValue(normalizedHotkey, out var existingId))
                {
                    // 更新动作
                    _hotkeyActions[existingId] = action;
                    return true;
                }

                var id = _nextId++;
                var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

                if (RegisterHotKey(_windowHandle, id, modifiers, vk))
                {
                    _hotkeyActions[id] = action;
                    _hotkeyToId[normalizedHotkey] = id;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool UnregisterHotkey(string hotkey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hotkey))
                    return false;

                var normalizedHotkey = NormalizeHotkey(hotkey);

                if (_hotkeyToId.TryGetValue(normalizedHotkey, out var id))
                {
                    UnregisterHotKey(_windowHandle, id);
                    _hotkeyActions.Remove(id);
                    _hotkeyToId.Remove(normalizedHotkey);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool IsHotkeyRegistered(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
                return false;

            var normalizedHotkey = NormalizeHotkey(hotkey);
            return _hotkeyToId.ContainsKey(normalizedHotkey);
        }

        public void UnregisterAll()
        {
            foreach (var id in _hotkeyActions.Keys.ToList())
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _hotkeyActions.Clear();
            _hotkeyToId.Clear();
            _nextId = 1;
        }

        public void ProcessHotkey(int id)
        {
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action?.Invoke();
            }
        }

        public static bool ValidateHotkey(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
                return false;

            var parts = hotkey.Split('+').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            if (parts.Count < 1)
                return false;

            var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? regularKey = null;

            foreach (var part in parts)
            {
                if (IsModifierKey(part))
                {
                    // 检查重复修饰键
                    if (!modifiers.Add(part))
                        return false;
                }
                else
                {
                    if (regularKey != null)
                        return false; // 多个普通键
                    regularKey = part;
                }
            }

            // 如果没有修饰键，必须是 F1-F12 功能键
            if (modifiers.Count == 0)
            {
                return regularKey != null && IsFunctionKey(regularKey);
            }

            return regularKey != null && ParseKey(hotkey) != Key.None;
        }

        private static bool IsFunctionKey(string key)
        {
            return key.Equals("F1", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F2", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F3", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F4", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F5", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F6", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F7", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F8", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F9", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F10", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F11", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("F12", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeHotkey(string hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
                return string.Empty;

            var parts = hotkey.Split('+').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            // 定义修饰键顺序
            var modifierOrder = new[] { "Ctrl", "Alt", "Shift", "Win" };
            var modifiers = new List<string>();
            string? regularKey = null;

            foreach (var part in parts)
            {
                if (IsModifierKey(part))
                {
                    var normalized = part.Equals("Windows", StringComparison.OrdinalIgnoreCase) ? "Win" : part;
                    if (!modifiers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        modifiers.Add(normalized);
                    }
                }
                else
                {
                    regularKey = part;
                }
            }

            // 按固定顺序排序修饰键
            var orderedModifiers = modifierOrder
                .Where(m => modifiers.Contains(m, StringComparer.OrdinalIgnoreCase))
                .Select(m => m);

            var result = new List<string>(orderedModifiers);
            if (regularKey != null)
            {
                result.Add(regularKey);
            }

            return string.Join("+", result);
        }

        private static bool IsModifierKey(string key)
        {
            return key.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        }

        private uint ParseModifiers(string hotkey)
        {
            var modifiers = 0u;
            var parts = hotkey.Split('+');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_CTRL;
                else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_ALT;
                else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_SHIFT;
                else if (trimmed.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    modifiers |= MOD_WIN;
            }

            return modifiers;
        }

        private static Key ParseKey(string hotkey)
        {
            var parts = hotkey.Split('+');
            var keyPart = parts.LastOrDefault()?.Trim();

            if (string.IsNullOrEmpty(keyPart))
                return Key.None;

            // 直接尝试解析
            if (Enum.TryParse<Key>(keyPart, true, out var key))
            {
                // 确保不是修饰键
                if (!IsModifierKey(keyPart))
                    return key;
            }

            // 处理单个字符
            if (keyPart.Length == 1)
            {
                var ch = char.ToUpper(keyPart[0]);

                // 处理字母 A-Z
                if (ch >= 'A' && ch <= 'Z')
                {
                    return KeyInterop.KeyFromVirtualKey((int)ch);
                }

                // 处理数字 0-9
                if (ch >= '0' && ch <= '9')
                {
                    if (Enum.TryParse<Key>($"D{ch}", out var digitKey))
                        return digitKey;
                }
            }

            // 处理特殊键名映射
            var keyMappings = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
            {
                ["0"] = Key.D0, ["1"] = Key.D1, ["2"] = Key.D2, ["3"] = Key.D3, ["4"] = Key.D4,
                ["5"] = Key.D5, ["6"] = Key.D6, ["7"] = Key.D7, ["8"] = Key.D8, ["9"] = Key.D9,
                ["Enter"] = Key.Enter, ["Return"] = Key.Enter,
                ["Space"] = Key.Space, [" "] = Key.Space,
                ["Tab"] = Key.Tab,
                ["Esc"] = Key.Escape, ["Escape"] = Key.Escape,
                ["Up"] = Key.Up, ["Down"] = Key.Down, ["Left"] = Key.Left, ["Right"] = Key.Right,
                ["Home"] = Key.Home, ["End"] = Key.End,
                ["PgUp"] = Key.PageUp, ["PageUp"] = Key.PageUp,
                ["PgDn"] = Key.PageDown, ["PageDown"] = Key.PageDown,
                ["Ins"] = Key.Insert, ["Insert"] = Key.Insert,
                ["Del"] = Key.Delete, ["Delete"] = Key.Delete,
                ["Back"] = Key.Back, ["Backspace"] = Key.Back,
                ["F1"] = Key.F1, ["F2"] = Key.F2, ["F3"] = Key.F3, ["F4"] = Key.F4,
                ["F5"] = Key.F5, ["F6"] = Key.F6, ["F7"] = Key.F7, ["F8"] = Key.F8,
                ["F9"] = Key.F9, ["F10"] = Key.F10, ["F11"] = Key.F11, ["F12"] = Key.F12,
                [","] = Key.OemComma, ["Comma"] = Key.OemComma,
                ["."] = Key.OemPeriod, ["Period"] = Key.OemPeriod,
                ["/"] = Key.OemQuestion, ["Question"] = Key.OemQuestion,
                [";"] = Key.OemSemicolon, ["Semicolon"] = Key.OemSemicolon,
                ["'"] = Key.OemQuotes, ["Quote"] = Key.OemQuotes,
                ["["] = Key.OemOpenBrackets, ["OpenBracket"] = Key.OemOpenBrackets,
                ["]"] = Key.OemCloseBrackets, ["CloseBracket"] = Key.OemCloseBrackets,
                ["-"] = Key.OemMinus, ["Minus"] = Key.OemMinus,
                ["="] = Key.OemPlus, ["Plus"] = Key.OemPlus,
                ["`"] = Key.OemTilde, ["Tilde"] = Key.OemTilde,
                ["\\"] = Key.OemBackslash, ["Backslash"] = Key.OemBackslash,
            };

            if (keyMappings.TryGetValue(keyPart, out var mappedKey))
            {
                return mappedKey;
            }

            return Key.None;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterAll();
                _disposed = true;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
    }
}
