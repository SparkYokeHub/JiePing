using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class HotkeyManager : IDisposable
    {
        private static volatile bool _suspended;
        private static HotkeyManager _current;

        private readonly Native.LowLevelKeyboardProc _proc;
        private readonly Timer _tapTimer;
        private readonly Timer _holdTimer;
        private readonly Action _onTrigger;
        private readonly HashSet<int> _down = new HashSet<int>();
        private readonly Random _random = new Random();
        private HotkeyConfig _config;
        private IntPtr _hook = IntPtr.Zero;
        private bool _tapArmed;
        private bool _tapPending;
        private bool _fired;
        private bool _disposed;
        private int _lastTriggerTick;

        public HotkeyManager(Action onTrigger)
        {
            _current = this;
            _onTrigger = onTrigger;
            _config = SettingsStore.GetHotkey();
            _proc = HookCallback;

            _tapTimer = new Timer();
            _tapTimer.Tick += TapTimerTick;

            _holdTimer = new Timer();
            _holdTimer.Interval = HotkeyConfig.DefaultHoldMs;
            _holdTimer.Tick += HoldTimerTick;
        }

        internal static bool Suspended
        {
            get { return _suspended; }
            set { _suspended = value; }
        }

        internal static void ResetForEditor()
        {
            HotkeyManager manager = _current;
            if (manager != null && !manager._disposed)
            {
                manager.ResetState();
            }
        }

        public HotkeyConfig Config
        {
            get { return _config; }
        }

        public bool Install()
        {
            if (_hook != IntPtr.Zero)
            {
                return true;
            }
            try
            {
                IntPtr module = Native.GetModuleHandle(null);
                _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, module, 0);
                if (_hook == IntPtr.Zero)
                {
                    CaptureStore.Log("keyboard hook failed: " + Marshal.GetLastWin32Error());
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                CaptureStore.Log("keyboard hook exception: " + ex.Message);
                return false;
            }
        }

        public void ReloadConfig()
        {
            _config = SettingsStore.GetHotkey();
            ResetState();
        }

        private void ResetState()
        {
            _tapTimer.Stop();
            _holdTimer.Stop();
            _down.Clear();
            _tapArmed = false;
            _tapPending = false;
            _fired = false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int message = wParam.ToInt32();
                    bool down = message == Native.WM_KEYDOWN || message == Native.WM_SYSKEYDOWN;
                    bool up = message == Native.WM_KEYUP || message == Native.WM_SYSKEYUP;
                    if (down || up)
                    {
                        Native.KBDLLHOOKSTRUCT data = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(
                            lParam, typeof(Native.KBDLLHOOKSTRUCT));
                        int vk = (int)data.vkCode;
                        if (down)
                        {
                            if (!HotkeyNames.ContainsKey(_down, vk))
                            {
                                _down.Add(vk);
                                OnKeyDown(vk);
                            }
                        }
                        else
                        {
                            if (HotkeyNames.RemoveKey(_down, vk))
                            {
                                OnKeyUp();
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void OnKeyDown(int vk)
        {
            bool complete = IsChordDown();
            if (complete)
            {
                _tapArmed = true;
                if (_config.Mode == HotkeyTriggerMode.Hold)
                {
                    if (!_holdTimer.Enabled)
                    {
                        _holdTimer.Start();
                    }
                }
            }
            else
            {
                _tapArmed = false;
                _holdTimer.Stop();
            }
        }

        private void OnKeyUp()
        {
            if (_down.Count == 0)
            {
                if (_config.Mode == HotkeyTriggerMode.Tap && _tapArmed && !_tapPending)
                {
                    ScheduleTap();
                }
                _tapArmed = false;
                _fired = false;
                _holdTimer.Stop();
            }
            else if (!IsChordDown())
            {
                _holdTimer.Stop();
            }
        }

        private void ScheduleTap()
        {
            if (_disposed || _tapPending)
            {
                return;
            }
            int min = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _config.MinDelayMs));
            int max = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _config.MaxDelayMsValue));
            if (max < min)
            {
                int swap = min;
                min = max;
                max = swap;
            }
            int delay = max <= min ? min : _random.Next(min, max + 1);
            _tapPending = true;
            _tapTimer.Stop();
            _tapTimer.Interval = Math.Max(1, delay);
            _tapTimer.Start();
        }

        private void TapTimerTick(object sender, EventArgs e)
        {
            _tapTimer.Stop();
            _tapPending = false;
            Trigger();
        }

        private void HoldTimerTick(object sender, EventArgs e)
        {
            _holdTimer.Stop();
            if (_disposed || _fired || !IsChordDown())
            {
                return;
            }
            _fired = true;
            Trigger();
        }

        private void Trigger()
        {
            if (_disposed || Suspended)
            {
                return;
            }
            int now = Environment.TickCount;
            if (_lastTriggerTick != 0 && unchecked(now - _lastTriggerTick) < 400)
            {
                return;
            }
            _lastTriggerTick = now;
            try
            {
                _onTrigger();
            }
            catch (Exception ex)
            {
                CaptureStore.Log("hotkey trigger failed: " + ex.Message);
            }
        }

        private bool IsChordDown()
        {
            bool ctrl = false;
            bool alt = false;
            bool shift = false;
            bool win = false;
            int mainCount = 0;
            bool mainMatch = false;

            foreach (int vk in _down)
            {
                if (HotkeyNames.IsControl(vk))
                {
                    ctrl = true;
                }
                else if (HotkeyNames.IsAlt(vk))
                {
                    alt = true;
                }
                else if (HotkeyNames.IsShift(vk))
                {
                    shift = true;
                }
                else if (HotkeyNames.IsWin(vk))
                {
                    win = true;
                }
                else
                {
                    mainCount++;
                    if (vk == _config.KeyCode)
                    {
                        mainMatch = true;
                    }
                }
            }

            if (ctrl != _config.Ctrl || alt != _config.Alt ||
                shift != _config.Shift || win != _config.Win)
            {
                return false;
            }
            if (_config.KeyCode == 0)
            {
                return mainCount == 0;
            }
            return mainCount == 1 && mainMatch;
        }

        public void Dispose()
        {
            _disposed = true;
            _tapTimer.Stop();
            _tapTimer.Dispose();
            _holdTimer.Stop();
            _holdTimer.Dispose();
            if (_hook != IntPtr.Zero)
            {
                try
                {
                    Native.UnhookWindowsHookEx(_hook);
                }
                catch
                {
                }
                _hook = IntPtr.Zero;
            }
        }
    }
}
