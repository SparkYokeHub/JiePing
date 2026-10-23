using System;
using System.Windows.Forms;

namespace JiePing
{
    internal enum HotkeyTriggerMode
    {
        Tap,
        Hold
    }

    internal sealed class HotkeyConfig
    {
        public const int DefaultHoldMs = 600;
        public const int MaxDelayMs = 5000;

        public bool Ctrl;
        public bool Alt;
        public bool Shift;
        public bool Win;
        public int KeyCode;
        public HotkeyTriggerMode Mode;
        public int MinDelayMs;
        public int MaxDelayMsValue;

        public static HotkeyConfig CreateDefault()
        {
            HotkeyConfig config = new HotkeyConfig();
            config.Ctrl = true;
            config.Alt = true;
            config.KeyCode = 0;
            config.Mode = HotkeyTriggerMode.Tap;
            config.MinDelayMs = 0;
            config.MaxDelayMsValue = 120;
            return config;
        }

        public int ModifierCount
        {
            get
            {
                int count = 0;
                if (Ctrl) count++;
                if (Alt) count++;
                if (Shift) count++;
                if (Win) count++;
                return count;
            }
        }

        public bool IsValid
        {
            get
            {
                if (KeyCode != 0)
                {
                    if (Ctrl || Alt || Shift || Win)
                    {
                        return true;
                    }
                    return HotkeyNames.IsStandaloneKey(KeyCode);
                }
                return ModifierCount >= 2;
            }
        }

        public string DisplayText
        {
            get
            {
                System.Collections.Generic.List<string> parts =
                    new System.Collections.Generic.List<string>();
                if (Ctrl) parts.Add("Ctrl");
                if (Alt) parts.Add("Alt");
                if (Shift) parts.Add("Shift");
                if (Win) parts.Add("Win");
                if (KeyCode != 0)
                {
                    parts.Add(HotkeyNames.Name(KeyCode));
                }
                if (parts.Count == 0)
                {
                    return Loc.T("未设置", "Not set");
                }
                return string.Join(" + ", parts.ToArray());
            }
        }

        public string Summary
        {
            get
            {
                if (Mode == HotkeyTriggerMode.Hold)
                {
                    return Loc.T("长按触发 · 约 ", "Hold · about ") +
                        (DefaultHoldMs / 100.0).ToString("0.0") +
                        Loc.T(" 秒", " s");
                }
                return Loc.T("点击触发 · ", "Tap · ") + MinDelayMs + "–" + MaxDelayMsValue + " ms";
            }
        }

        public HotkeyConfig Clone()
        {
            HotkeyConfig copy = new HotkeyConfig();
            copy.Ctrl = Ctrl;
            copy.Alt = Alt;
            copy.Shift = Shift;
            copy.Win = Win;
            copy.KeyCode = KeyCode;
            copy.Mode = Mode;
            copy.MinDelayMs = MinDelayMs;
            copy.MaxDelayMsValue = MaxDelayMsValue;
            return copy;
        }

        public bool SameKeys(HotkeyConfig other)
        {
            return other != null &&
                   Ctrl == other.Ctrl &&
                   Alt == other.Alt &&
                   Shift == other.Shift &&
                   Win == other.Win &&
                   KeyCode == other.KeyCode;
        }

        public bool SameAs(HotkeyConfig other)
        {
            return SameKeys(other) &&
                   Mode == other.Mode &&
                   MinDelayMs == other.MinDelayMs &&
                   MaxDelayMsValue == other.MaxDelayMsValue;
        }
    }

    internal static class HotkeyNames
    {
        public static bool IsModifier(int vk)
        {
            return vk == 0x11 || vk == 0xA2 || vk == 0xA3 ||
                   vk == 0x12 || vk == 0xA4 || vk == 0xA5 ||
                   vk == 0x10 || vk == 0xA0 || vk == 0xA1 ||
                   vk == 0x5B || vk == 0x5C;
        }

        public static bool IsStandaloneKey(int vk)
        {
            if (vk >= 0x70 && vk <= 0x87)
            {
                return true;
            }
            if (vk >= 0xA6 && vk <= 0xB3)
            {
                return true;
            }
            return vk == 0x2C || vk == 0x91 || vk == 0x13;
        }

        public static bool IsControl(int vk)
        {
            return vk == 0x11 || vk == 0xA2 || vk == 0xA3;
        }

        public static bool IsAlt(int vk)
        {
            return vk == 0x12 || vk == 0xA4 || vk == 0xA5;
        }

        public static bool IsShift(int vk)
        {
            return vk == 0x10 || vk == 0xA0 || vk == 0xA1;
        }

        public static bool IsWin(int vk)
        {
            return vk == 0x5B || vk == 0x5C;
        }

        public static bool SameKey(int a, int b)
        {
            if (IsControl(a) && IsControl(b)) return true;
            if (IsAlt(a) && IsAlt(b)) return true;
            if (IsShift(a) && IsShift(b)) return true;
            if (IsWin(a) && IsWin(b)) return true;
            return a == b;
        }

        public static bool ContainsKey(System.Collections.Generic.HashSet<int> set, int vk)
        {
            foreach (int item in set)
            {
                if (SameKey(item, vk))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool RemoveKey(System.Collections.Generic.HashSet<int> set, int vk)
        {
            System.Collections.Generic.List<int> matches =
                new System.Collections.Generic.List<int>();
            foreach (int item in set)
            {
                if (SameKey(item, vk))
                {
                    matches.Add(item);
                }
            }
            bool removed = false;
            foreach (int item in matches)
            {
                removed |= set.Remove(item);
            }
            return removed;
        }

        public static string Name(int vk)
        {
            if (vk <= 0)
            {
                return string.Empty;
            }
            if (vk >= (int)Keys.A && vk <= (int)Keys.Z)
            {
                return ((char)('A' + (vk - (int)Keys.A))).ToString();
            }
            if (vk >= (int)Keys.D0 && vk <= (int)Keys.D9)
            {
                return ((char)('0' + (vk - (int)Keys.D0))).ToString();
            }
            if (vk >= (int)Keys.NumPad0 && vk <= (int)Keys.NumPad9)
            {
                return "Num " + (vk - (int)Keys.NumPad0);
            }
            if (vk >= (int)Keys.F1 && vk <= (int)Keys.F24)
            {
                return "F" + (vk - (int)Keys.F1 + 1);
            }

            switch ((Keys)vk)
            {
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.ControlKey:
                    return "Ctrl";
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.Menu:
                    return "Alt";
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ShiftKey:
                    return "Shift";
                case Keys.LWin:
                case Keys.RWin:
                    return "Win";
                case Keys.Back:
                    return "Backspace";
                case Keys.Capital:
                    return "Caps";
                case Keys.Escape:
                    return "Esc";
                case Keys.Space:
                    return "Space";
                case Keys.Enter:
                    return "Enter";
                case Keys.Tab:
                    return "Tab";
                case Keys.Delete:
                    return "Delete";
                case Keys.Insert:
                    return "Insert";
                case Keys.Home:
                    return "Home";
                case Keys.End:
                    return "End";
                case Keys.PageUp:
                    return "PageUp";
                case Keys.PageDown:
                    return "PageDown";
                case Keys.Up:
                    return "↑";
                case Keys.Down:
                    return "↓";
                case Keys.Left:
                    return "←";
                case Keys.Right:
                    return "→";
                case Keys.Oemtilde:
                    return "`";
                case Keys.OemMinus:
                    return "-";
                case Keys.Oemplus:
                    return "=";
                case Keys.OemOpenBrackets:
                    return "[";
                case Keys.OemCloseBrackets:
                    return "]";
                case Keys.OemPipe:
                    return "\\";
                case Keys.OemSemicolon:
                    return ";";
                case Keys.OemQuotes:
                    return "'";
                case Keys.Oemcomma:
                    return ",";
                case Keys.OemPeriod:
                    return ".";
                case Keys.OemQuestion:
                    return "/";
                case Keys.Apps:
                    return "Menu";
                case Keys.PrintScreen:
                    return "PrtSc";
                case Keys.Scroll:
                    return "ScrLk";
                case Keys.Pause:
                    return "Pause";
                case Keys.MediaNextTrack:
                    return "Next";
                case Keys.MediaPreviousTrack:
                    return "Prev";
                case Keys.MediaStop:
                    return "Stop";
                case Keys.MediaPlayPause:
                    return "Play";
                case Keys.VolumeMute:
                    return "Mute";
                case Keys.VolumeDown:
                    return "Vol-";
                case Keys.VolumeUp:
                    return "Vol+";
            }
            return ((Keys)vk).ToString();
        }
    }
}
