using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class ShortcutWizard : Form
    {
        private enum WizardStep
        {
            Keys,
            Trigger
        }

        private enum HoverButton
        {
            None,
            Cancel,
            Retry,
            Next,
            Back,
            Save
        }

        private sealed class KeyDef
        {
            public int Vk;
            public string Label;
            public int Row;
            public float X;
            public float W;

            public KeyDef(int vk, int row, float x, float w)
            {
                Vk = vk;
                Row = row;
                X = x;
                W = w;
                Label = HotkeyNames.Name(vk);
            }
        }

        private readonly HotkeyConfig _current;
        private HotkeyConfig _candidate;
        private HotkeyConfig _result;
        private HotkeyTriggerMode _mode;
        private WizardStep _step = WizardStep.Keys;
        private readonly List<KeyDef> _keys = new List<KeyDef>();
        private readonly HashSet<int> _down = new HashSet<int>();
        private HashSet<int> _candidateKeys = new HashSet<int>();
        private int _lastMainVk;
        private Native.LowLevelKeyboardProc _hookProc;
        private IntPtr _hook;
        private bool _hookInstalled;

        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _comboFont;
        private readonly Font _comboLabelFont;
        private readonly Font _keyFont;
        private readonly Font _keySmallFont;
        private readonly Font _buttonFont;
        private readonly Font _cardTitleFont;
        private readonly Font _cardBodyFont;
        private readonly Font _smallFont;

        private readonly TextBox _minBox;
        private readonly TextBox _maxBox;
        private float _dpi = 1f;
        private Rectangle _comboRect;
        private Rectangle _keyboardRect;
        private Rectangle _cancelRect;
        private Rectangle _retryRect;
        private Rectangle _nextRect;
        private Rectangle _backRect;
        private Rectangle _saveRect;
        private Rectangle _tapRect;
        private Rectangle _holdRect;
        private HoverButton _hover;
        private int _hoverMode = -1;
        private string _error = string.Empty;

        public HotkeyConfig SelectedConfig
        {
            get { return _result; }
        }

        public ShortcutWizard(HotkeyConfig current, Icon appIcon)
        {
            _current = current == null ? HotkeyConfig.CreateDefault() : current.Clone();
            _candidate = _current.Clone();
            _mode = _current.Mode;
            _lastMainVk = _candidate.KeyCode;
            _candidateKeys = CandidateKeys(_candidate);

            Text = Loc.T("自定义快捷键", "Custom Shortcut");
            Icon = appIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 700);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = UiTheme.Background;
            ForeColor = UiTheme.Text;
            Font = UiTheme.UiFont(9f, FontStyle.Regular);
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _titleFont = UiTheme.UiFont(18f, FontStyle.Bold);
            _subFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _comboFont = UiTheme.UiFont(21f, FontStyle.Bold);
            _comboLabelFont = UiTheme.UiFont(8.5f, FontStyle.Regular);
            _keyFont = UiTheme.UiFont(7.2f, FontStyle.Regular);
            _keySmallFont = UiTheme.UiFont(6.2f, FontStyle.Regular);
            _buttonFont = UiTheme.UiFont(9.5f, FontStyle.Bold);
            _cardTitleFont = UiTheme.UiFont(13f, FontStyle.Bold);
            _cardBodyFont = UiTheme.UiFont(9f, FontStyle.Regular);
            _smallFont = UiTheme.UiFont(8.5f, FontStyle.Regular);

            _minBox = CreateDelayBox();
            _maxBox = CreateDelayBox();
            _minBox.Text = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _current.MinDelayMs)).ToString();
            _maxBox.Text = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _current.MaxDelayMsValue)).ToString();
            _minBox.TextChanged += delegate { _error = string.Empty; Invalidate(); };
            _maxBox.TextChanged += delegate { _error = string.Empty; Invalidate(); };
            _minBox.Visible = false;
            _maxBox.Visible = false;
            Controls.Add(_minBox);
            Controls.Add(_maxBox);

            BuildKeys();
            RecalcLayout();
        }

        private TextBox CreateDelayBox()
        {
            TextBox box = new TextBox();
            box.Width = (int)(86 * _dpi);
            box.MaxLength = 4;
            box.BackColor = Color.FromArgb(17, 19, 24);
            box.ForeColor = UiTheme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.TextAlign = HorizontalAlignment.Right;
            box.Font = UiTheme.UiFont(9.5f, FontStyle.Regular);
            box.TabStop = true;
            box.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };
            return box;
        }

        private int ReadDelay(TextBox box, int fallback)
        {
            int value;
            if (box != null && int.TryParse(box.Text, out value))
            {
                return Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, value));
            }
            return fallback;
        }

        private void BuildKeys()
        {
            AddKey(Keys.Escape, 0, 0f, 1.1f);
            for (int i = 0; i < 12; i++)
            {
                AddKey((Keys)((int)Keys.F1 + i), 0, 1.55f + i * 0.98f, 0.9f);
            }
            AddKey(Keys.PrintScreen, 0, 15.75f, 0.9f);
            AddKey(Keys.Scroll, 0, 16.72f, 0.9f);
            AddKey(Keys.Pause, 0, 17.69f, 0.9f);

            AddKey(Keys.Oemtilde, 1, 0f, 0.9f);
            for (int i = 0; i < 9; i++)
            {
                AddKey((Keys)((int)Keys.D1 + i), 1, 0.97f + i * 0.84f, 0.8f);
            }
            AddKey(Keys.D0, 1, 0.97f + 9 * 0.84f, 0.8f);
            AddKey(Keys.OemMinus, 1, 9.41f, 0.8f);
            AddKey(Keys.Oemplus, 1, 10.27f, 0.8f);
            AddKey(Keys.Back, 1, 11.13f, 2.0f);
            AddKey(Keys.Insert, 1, 15.75f, 0.9f, "Ins");
            AddKey(Keys.Home, 1, 16.72f, 0.9f);
            AddKey(Keys.PageUp, 1, 17.69f, 0.9f, "PgUp");

            AddKey(Keys.Tab, 2, 0f, 1.35f);
            for (int i = 0; i < 10; i++)
            {
                AddKey((Keys)((int)Keys.Q + i), 2, 1.42f + i * 0.84f, 0.8f);
            }
            AddKey(Keys.OemOpenBrackets, 2, 9.86f, 0.8f);
            AddKey(Keys.OemCloseBrackets, 2, 10.72f, 0.8f);
            AddKey(Keys.OemPipe, 2, 11.58f, 1.2f);
            AddKey(Keys.Delete, 2, 15.75f, 0.9f, "Del");
            AddKey(Keys.End, 2, 16.72f, 0.9f);
            AddKey(Keys.PageDown, 2, 17.69f, 0.9f, "PgDn");

            AddKey(Keys.Capital, 3, 0f, 1.55f);
            for (int i = 0; i < 12; i++)
            {
                AddKey((Keys)((int)Keys.A + i), 3, 1.62f + i * 0.82f, 0.78f);
            }
            AddKey(Keys.OemSemicolon, 3, 11.5f, 0.78f);
            AddKey(Keys.OemQuotes, 3, 12.34f, 0.78f);
            AddKey(Keys.Enter, 3, 13.18f, 2.0f);

            AddKey(Keys.LShiftKey, 4, 0f, 2.0f);
            AddKey(Keys.Z, 4, 2.07f, 0.78f);
            AddKey(Keys.X, 4, 2.89f, 0.78f);
            AddKey(Keys.C, 4, 3.71f, 0.78f);
            AddKey(Keys.V, 4, 4.53f, 0.78f);
            AddKey(Keys.B, 4, 5.35f, 0.78f);
            AddKey(Keys.N, 4, 6.17f, 0.78f);
            AddKey(Keys.M, 4, 6.99f, 0.78f);
            AddKey(Keys.Oemcomma, 4, 7.85f, 0.78f);
            AddKey(Keys.OemPeriod, 4, 8.69f, 0.78f);
            AddKey(Keys.OemQuestion, 4, 9.53f, 0.78f);
            AddKey(Keys.RShiftKey, 4, 10.37f, 2.3f);
            AddKey(Keys.Up, 4, 16.72f, 0.9f);

            AddKey(Keys.LControlKey, 5, 0f, 1.3f);
            AddKey(Keys.LWin, 5, 1.37f, 1.0f);
            AddKey(Keys.LMenu, 5, 2.43f, 1.1f);
            AddKey(Keys.Space, 5, 3.59f, 5.8f);
            AddKey(Keys.RMenu, 5, 9.45f, 1.1f);
            AddKey(Keys.RWin, 5, 10.61f, 1.0f);
            AddKey(Keys.Apps, 5, 11.67f, 1.0f);
            AddKey(Keys.RControlKey, 5, 12.73f, 1.45f);
            AddKey(Keys.Left, 5, 15.75f, 0.9f);
            AddKey(Keys.Down, 5, 16.72f, 0.9f);
            AddKey(Keys.Right, 5, 17.69f, 0.9f);
        }

        private void AddKey(Keys key, int row, float x, float w)
        {
            _keys.Add(new KeyDef((int)key, row, x, w));
        }

        private void AddKey(Keys key, int row, float x, float w, string label)
        {
            KeyDef def = new KeyDef((int)key, row, x, w);
            def.Label = label;
            _keys.Add(def);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.EnableDarkTitleBar(Handle);
            using (Graphics g = CreateGraphics())
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            _minBox.Width = (int)(86 * _dpi);
            _maxBox.Width = (int)(86 * _dpi);
            RecalcLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            HotkeyManager.Suspended = true;
            HotkeyManager.ResetForEditor();
            InstallHook();
            Native.ForceForeground(Handle);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcLayout();
        }

        private void RecalcLayout()
        {
            if (_minBox == null || _maxBox == null)
            {
                return;
            }
            int left = (int)(40 * _dpi);
            int width = ClientSize.Width - left * 2;
            _comboRect = new Rectangle(left, (int)(74 * _dpi), width, (int)(62 * _dpi));
            _keyboardRect = new Rectangle(left, (int)(158 * _dpi), width, (int)(322 * _dpi));

            int bottom = ClientSize.Height - (int)(66 * _dpi);
            _cancelRect = new Rectangle(left, bottom, (int)(86 * _dpi), (int)(40 * _dpi));
            _nextRect = new Rectangle(ClientSize.Width - left - (int)(104 * _dpi), bottom,
                (int)(104 * _dpi), (int)(40 * _dpi));
            _retryRect = new Rectangle(_nextRect.Left - (int)(118 * _dpi), bottom,
                (int)(108 * _dpi), (int)(40 * _dpi));
            _saveRect = _nextRect;
            _backRect = new Rectangle(_nextRect.Left - (int)(108 * _dpi), bottom,
                (int)(98 * _dpi), (int)(40 * _dpi));

            int modeTop = (int)(138 * _dpi);
            _tapRect = new Rectangle(left, modeTop, (int)(390 * _dpi), (int)(104 * _dpi));
            _holdRect = new Rectangle(left + (int)(410 * _dpi), modeTop,
                (int)(390 * _dpi), (int)(104 * _dpi));

            int boxY = (int)(306 * _dpi);
            _minBox.Location = new Point(left + (int)(168 * _dpi), boxY);
            _maxBox.Location = new Point(left + (int)(360 * _dpi), boxY);
            _minBox.Visible = _step == WizardStep.Trigger && _mode == HotkeyTriggerMode.Tap;
            _maxBox.Visible = _step == WizardStep.Trigger && _mode == HotkeyTriggerMode.Tap;
        }

        private void InstallHook()
        {
            if (_hookInstalled)
            {
                return;
            }
            try
            {
                _hookProc = HookCallback;
                _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc,
                    Native.GetModuleHandle(null), 0);
                _hookInstalled = _hook != IntPtr.Zero;
                if (!_hookInstalled)
                {
                    CaptureStore.Log("shortcut recorder hook failed: " + Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                CaptureStore.Log("shortcut recorder hook exception: " + ex.Message);
            }
        }

        private void UninstallHook()
        {
            if (!_hookInstalled)
            {
                return;
            }
            try
            {
                Native.UnhookWindowsHookEx(_hook);
            }
            catch
            {
            }
            _hook = IntPtr.Zero;
            _hookInstalled = false;
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
                        if (vk == Native.VK_ESCAPE)
                        {
                            if (down)
                            {
                                BeginInvoke(new Action(delegate { Close(); }));
                            }
                            return (IntPtr)1;
                        }
                        if (down)
                        {
                            RecordKeyDown(vk);
                        }
                        else
                        {
                            RecordKeyUp(vk);
                        }
                    }
                }
                catch
                {
                }
            }
            return (IntPtr)1;
        }

        private void RecordKeyDown(int vk)
        {
            bool wasEmpty = _down.Count == 0;
            if (wasEmpty)
            {
                _candidate = null;
                _lastMainVk = 0;
                _candidateKeys.Clear();
            }
            if (!HotkeyNames.ContainsKey(_down, vk))
            {
                _down.Add(vk);
                if (!HotkeyNames.IsModifier(vk))
                {
                    _lastMainVk = vk;
                }
                RebuildCandidate();
                Invalidate();
            }
        }

        private void RecordKeyUp(int vk)
        {
            if (HotkeyNames.RemoveKey(_down, vk))
            {
                Invalidate();
            }
        }

        private void RebuildCandidate()
        {
            HotkeyConfig config = new HotkeyConfig();
            config.Ctrl = false;
            config.Alt = false;
            config.Shift = false;
            config.Win = false;
            foreach (int vk in _down)
            {
                if (HotkeyNames.IsControl(vk)) config.Ctrl = true;
                else if (HotkeyNames.IsAlt(vk)) config.Alt = true;
                else if (HotkeyNames.IsShift(vk)) config.Shift = true;
                else if (HotkeyNames.IsWin(vk)) config.Win = true;
            }
            config.KeyCode = _lastMainVk;
            config.Mode = HotkeyTriggerMode.Tap;
            config.MinDelayMs = ReadDelay(_minBox, 0);
            config.MaxDelayMsValue = ReadDelay(_maxBox, 120);
            _candidate = config;
            _candidateKeys = CandidateKeys(config);
        }

        private static HashSet<int> CandidateKeys(HotkeyConfig config)
        {
            HashSet<int> set = new HashSet<int>();
            if (config.Ctrl) set.Add((int)Keys.LControlKey);
            if (config.Alt) set.Add((int)Keys.LMenu);
            if (config.Shift) set.Add((int)Keys.LShiftKey);
            if (config.Win) set.Add((int)Keys.LWin);
            if (config.KeyCode != 0) set.Add(config.KeyCode);
            return set;
        }

        private void ClearCandidate()
        {
            _candidate = null;
            _candidateKeys.Clear();
            _lastMainVk = 0;
            _down.Clear();
            _error = string.Empty;
            Invalidate();
        }

        private void GoStep2()
        {
            if (_candidate == null || !_candidate.IsValid)
            {
                return;
            }
            _step = WizardStep.Trigger;
            _error = string.Empty;
            UninstallHook();
            _minBox.Visible = _mode == HotkeyTriggerMode.Tap;
            _maxBox.Visible = _mode == HotkeyTriggerMode.Tap;
            _minBox.Text = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _candidate.MinDelayMs)).ToString();
            _maxBox.Text = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, _candidate.MaxDelayMsValue)).ToString();
            Invalidate();
        }

        private void GoStep1()
        {
            _step = WizardStep.Keys;
            _error = string.Empty;
            _minBox.Visible = false;
            _maxBox.Visible = false;
            InstallHook();
            Invalidate();
        }

        private void Save()
        {
            if (_candidate == null || !_candidate.IsValid)
            {
                _error = Loc.T("请先录入有效的快捷键。", "Record a valid shortcut first.");
                Invalidate();
                return;
            }
            int min;
            int max;
            if (!int.TryParse(_minBox.Text, out min) || !int.TryParse(_maxBox.Text, out max))
            {
                _error = Loc.T("延迟必须填写数字。", "Enter numbers for the delays.");
                Invalidate();
                return;
            }
            min = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, min));
            max = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, max));
            if (max < min)
            {
                _error = Loc.T("延迟最高值不能小于最低值。",
                    "The maximum delay cannot be lower than the minimum.");
                Invalidate();
                return;
            }
            HotkeyConfig result = _candidate.Clone();
            result.Mode = _mode;
            result.MinDelayMs = min;
            result.MaxDelayMsValue = max;
            _result = result;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(UiTheme.Background))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_step == WizardStep.Keys)
            {
                DrawKeysStep(g);
            }
            else
            {
                DrawTriggerStep(g);
            }
        }

        private void DrawKeysStep(Graphics g)
        {
            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("设置快捷键", "Set shortcut"), _titleFont, title,
                    (int)(38 * _dpi), (int)(22 * _dpi));
            }
            using (SolidBrush sub = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("按下你想使用的组合键，键盘上对应的按键会亮起。",
                        "Press the shortcut you want; the matching keys light up."), _subFont, sub,
                    (int)(40 * _dpi), (int)(54 * _dpi));
            }

            DrawComboBox(g);
            DrawKeyboard(g);

            string hint;
            if (_candidate == null)
            {
                hint = Loc.T("请按下至少两个键，或使用 F1–F12、媒体键作为单键快捷键。",
                    "Press at least two keys, or use F1–F12 or a media key alone.");
            }
            else if (!_candidate.IsValid)
            {
                hint = Loc.T("这个组合太容易误触：请加上 Ctrl/Alt/Shift/Win，或改用 F1–F12 等功能键。",
                    "Too easy to trigger by accident: add Ctrl/Alt/Shift/Win, or use F1–F12.");
            }
            else
            {
                hint = Loc.T("录入完成，点击「下一步」选择点击触发或长按触发。",
                    "Ready. Click Next to choose tap or hold trigger.");
            }
            if (!string.IsNullOrEmpty(_error))
            {
                hint = _error;
            }
            using (SolidBrush hb = new SolidBrush(string.IsNullOrEmpty(_error)
                ? UiTheme.Faint
                : Color.FromArgb(255, 116, 106)))
            {
                g.DrawString(hint, _smallFont, hb,
                    new RectangleF((int)(40 * _dpi), (int)(500 * _dpi),
                        ClientSize.Width - (int)(80 * _dpi), (int)(40 * _dpi)));
            }
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(100, 104, 114)))
            {
                g.DrawString(Loc.T("键盘图为主键盘区域；小键盘、F13–F24 和多媒体键也可以直接录入。",
                        "The diagram shows the main keyboard; numpad, F13–F24 and media keys also work."),
                    _smallFont, nb, (int)(40 * _dpi), (int)(526 * _dpi));
            }

            DrawButton(g, _cancelRect, Loc.T("取消", "Cancel"), false, false, _hover == HoverButton.Cancel);
            DrawButton(g, _retryRect, Loc.T("重新录入", "Redo"), false, false, _hover == HoverButton.Retry);
            DrawButton(g, _nextRect, Loc.T("下一步", "Next"), true, false,
                _hover == HoverButton.Next && _candidate != null && _candidate.IsValid,
                _candidate != null && _candidate.IsValid);
        }

        private void DrawTriggerStep(Graphics g)
        {
            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("选择触发方式", "Choose trigger mode"), _titleFont, title,
                    (int)(38 * _dpi), (int)(22 * _dpi));
            }
            using (SolidBrush sub = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("快捷键：", "Shortcut: "), _subFont, sub,
                    (int)(40 * _dpi), (int)(60 * _dpi));
            }
            using (Font comboFont = UiTheme.UiFont(11f, FontStyle.Bold))
            using (SolidBrush combo = new SolidBrush(UiTheme.Accent))
            {
                g.DrawString(_candidate == null ? Loc.T("未设置", "Not set") : _candidate.DisplayText,
                    comboFont, combo, (int)(104 * _dpi), (int)(57 * _dpi));
            }

            DrawModeCard(g, _tapRect, Loc.T("点击触发", "Tap trigger"),
                Loc.T("松开快捷键后，随机等待设定的延迟区间再截图。",
                    "After release, wait a random delay in the configured range."),
                _mode == HotkeyTriggerMode.Tap, _hoverMode == 0);
            DrawModeCard(g, _holdRect, Loc.T("长按触发", "Hold trigger"),
                Loc.T("按住快捷键约 0.6 秒后截图，适合防止误触。",
                    "Hold for about 0.6 s. Good for avoiding accidental shots."),
                _mode == HotkeyTriggerMode.Hold, _hoverMode == 1);

            if (_mode == HotkeyTriggerMode.Tap)
            {
                using (SolidBrush label = new SolidBrush(UiTheme.Text))
                {
                    g.DrawString(Loc.T("点击触发延迟", "Tap trigger delay"),
                        UiTheme.UiFont(10f, FontStyle.Bold), label,
                        (int)(40 * _dpi), (int)(268 * _dpi));
                }
                using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
                {
                    g.DrawString(Loc.T("最低", "Min"), _smallFont, muted,
                        (int)(130 * _dpi), (int)(312 * _dpi));
                    g.DrawString(Loc.T("最高", "Max"), _smallFont, muted,
                        (int)(322 * _dpi), (int)(312 * _dpi));
                    g.DrawString(Loc.T("毫秒（区间内随机）", "ms (random in range)"), _smallFont, muted,
                        (int)(510 * _dpi), (int)(312 * _dpi));
                }
            }
            else
            {
                using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
                {
                    g.DrawString(Loc.T("长按阈值固定为约 0.6 秒。若想调整点击延迟，请选择「点击触发」。",
                            "Hold threshold is fixed at about 0.6 s. Choose Tap to adjust the delay."),
                        _subFont, muted, (int)(40 * _dpi), (int)(292 * _dpi));
                }
            }

            if (!string.IsNullOrEmpty(_error))
            {
                using (SolidBrush error = new SolidBrush(Color.FromArgb(255, 116, 106)))
                {
                    g.DrawString(_error, _smallFont, error, (int)(40 * _dpi), (int)(370 * _dpi));
                }
            }

            DrawButton(g, _cancelRect, Loc.T("取消", "Cancel"), false, false, _hover == HoverButton.Cancel);
            DrawButton(g, _backRect, Loc.T("上一步", "Back"), false, false, _hover == HoverButton.Back);
            DrawButton(g, _saveRect, Loc.T("保存", "Save"), true, false, _hover == HoverButton.Save);
        }

        private void DrawComboBox(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_comboRect, 12f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(21, 23, 29)))
            using (Pen border = new Pen(Color.FromArgb(50, 54, 64)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush label = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("当前快捷键", "Current shortcut"), _comboLabelFont, label,
                    _comboRect.Left + (int)(18 * _dpi), _comboRect.Top + (int)(10 * _dpi));
            }
            string text = _candidate == null ? Loc.T("请按下组合键…", "Press a shortcut…") : _candidate.DisplayText;
            using (SolidBrush value = new SolidBrush(_candidate != null && _candidate.IsValid
                ? UiTheme.Text
                : UiTheme.Muted))
            {
                g.DrawString(text, _comboFont, value,
                    _comboRect.Left + (int)(18 * _dpi), _comboRect.Top + (int)(25 * _dpi));
            }
        }

        private void DrawKeyboard(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(
                new Rectangle(_keyboardRect.Left - (int)(12 * _dpi),
                    _keyboardRect.Top - (int)(12 * _dpi),
                    _keyboardRect.Width + (int)(24 * _dpi),
                    _keyboardRect.Height + (int)(24 * _dpi)),
                16f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(14, 16, 20)))
            using (Pen border = new Pen(Color.FromArgb(40, 44, 53)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            float unit = _keyboardRect.Width / 18.72f;
            float keyHeight = (float)(34 * _dpi);
            float rowGap = (float)(5 * _dpi);
            float totalHeight = 6 * keyHeight + 5 * rowGap;
            float top = _keyboardRect.Top + (_keyboardRect.Height - totalHeight) / 2f;

            for (int i = 0; i < _keys.Count; i++)
            {
                KeyDef key = _keys[i];
                float x = _keyboardRect.Left + key.X * unit;
                float y = top + key.Row * (keyHeight + rowGap);
                float w = key.W * unit - (float)(2 * _dpi);
                RectangleF rect = new RectangleF(x, y, Math.Max(8, w), keyHeight);

                bool down = _down.Contains(key.Vk);
                bool selected = _candidateKeys.Contains(key.Vk);
                Color fill;
                Color border;
                Color text;
                if (down)
                {
                    fill = UiTheme.Accent;
                    border = Color.FromArgb(255, 171, 145);
                    text = Color.FromArgb(40, 20, 15);
                }
                else if (selected)
                {
                    fill = Color.FromArgb(77, 43, 35);
                    border = UiTheme.Accent;
                    text = Color.FromArgb(255, 218, 206);
                }
                else
                {
                    fill = Color.FromArgb(26, 29, 36);
                    border = Color.FromArgb(52, 56, 66);
                    text = Color.FromArgb(190, 194, 203);
                }

                using (GraphicsPath path = UiTheme.RoundedRect(rect, 4.5f * _dpi))
                using (SolidBrush b = new SolidBrush(fill))
                using (Pen p = new Pen(border, down || selected ? 1.3f : 1f))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
                Font font = key.Label.Length > 4 ? _keySmallFont : _keyFont;
                using (SolidBrush tb = new SolidBrush(text))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(key.Label, font, tb, rect, sf);
                }
            }
        }

        private void DrawModeCard(Graphics g, Rectangle rect, string title, string body, bool selected, bool hover)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 12f * _dpi))
            using (SolidBrush fill = new SolidBrush(selected
                ? Color.FromArgb(42, 29, 27)
                : (hover ? Color.FromArgb(26, 29, 36) : Color.FromArgb(20, 22, 28))))
            using (Pen border = new Pen(selected ? UiTheme.Accent : Color.FromArgb(46, 50, 59), selected ? 1.6f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Rectangle dot = new Rectangle(rect.Left + (int)(20 * _dpi),
                rect.Top + (int)(20 * _dpi), (int)(16 * _dpi), (int)(16 * _dpi));
            using (Pen pen = new Pen(selected ? UiTheme.Accent : Color.FromArgb(104, 110, 122), 1.4f))
            using (SolidBrush fill = new SolidBrush(selected ? UiTheme.Accent : Color.Transparent))
            {
                g.DrawEllipse(pen, dot);
                if (selected)
                {
                    g.FillEllipse(fill, Rectangle.Inflate(dot, -4, -4));
                }
            }
            using (SolidBrush tb = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(title, _cardTitleFont, tb,
                    rect.Left + (int)(48 * _dpi), rect.Top + (int)(16 * _dpi));
            }
            using (SolidBrush bb = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(body, _cardBodyFont, bb,
                    new RectangleF(rect.Left + (int)(20 * _dpi), rect.Top + (int)(50 * _dpi),
                        rect.Width - (int)(40 * _dpi), rect.Height - (int)(58 * _dpi)));
            }
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, bool primary, bool danger, bool hover)
        {
            DrawButton(g, rect, text, primary, danger, hover, true);
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, bool primary, bool danger, bool hover, bool enabled)
        {
            Color fill;
            Color border;
            Color textColor;
            if (!enabled)
            {
                fill = Color.FromArgb(24, 27, 33);
                border = Color.FromArgb(39, 43, 51);
                textColor = UiTheme.Faint;
            }
            else if (primary)
            {
                fill = hover ? Color.FromArgb(255, 136, 108) : UiTheme.Accent;
                border = Color.FromArgb(255, 154, 130);
                textColor = Color.FromArgb(38, 20, 15);
            }
            else
            {
                fill = hover ? Color.FromArgb(42, 46, 56) : Color.FromArgb(27, 30, 37);
                border = Color.FromArgb(hover ? 88 : 54, 255, 255, 255);
                textColor = UiTheme.Text;
            }
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 9f * _dpi))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (SolidBrush tb = new SolidBrush(textColor))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, _buttonFont, tb, rect, sf);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            HoverButton hover = HoverButton.None;
            int modeHover = -1;
            if (_step == WizardStep.Keys)
            {
                if (_cancelRect.Contains(e.Location)) hover = HoverButton.Cancel;
                else if (_retryRect.Contains(e.Location)) hover = HoverButton.Retry;
                else if (_nextRect.Contains(e.Location) && _candidate != null && _candidate.IsValid) hover = HoverButton.Next;
            }
            else
            {
                if (_cancelRect.Contains(e.Location)) hover = HoverButton.Cancel;
                else if (_backRect.Contains(e.Location)) hover = HoverButton.Back;
                else if (_saveRect.Contains(e.Location)) hover = HoverButton.Save;
                if (_tapRect.Contains(e.Location)) modeHover = 0;
                else if (_holdRect.Contains(e.Location)) modeHover = 1;
            }
            if (hover != _hover || modeHover != _hoverMode)
            {
                _hover = hover;
                _hoverMode = modeHover;
                Cursor = hover != HoverButton.None || modeHover >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = HoverButton.None;
            _hoverMode = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_step == WizardStep.Keys)
            {
                if (_cancelRect.Contains(e.Location))
                {
                    Close();
                }
                else if (_retryRect.Contains(e.Location))
                {
                    ClearCandidate();
                }
                else if (_nextRect.Contains(e.Location) && _candidate != null && _candidate.IsValid)
                {
                    GoStep2();
                }
            }
            else
            {
                if (_tapRect.Contains(e.Location))
                {
                    _mode = HotkeyTriggerMode.Tap;
                    _error = string.Empty;
                    RecalcLayout();
                    Invalidate();
                }
                else if (_holdRect.Contains(e.Location))
                {
                    _mode = HotkeyTriggerMode.Hold;
                    _error = string.Empty;
                    RecalcLayout();
                    Invalidate();
                }
                else if (_cancelRect.Contains(e.Location))
                {
                    Close();
                }
                else if (_backRect.Contains(e.Location))
                {
                    GoStep1();
                }
                else if (_saveRect.Contains(e.Location))
                {
                    Save();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_step != WizardStep.Keys)
            {
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            RecordKeyDown(e.KeyValue);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (_step == WizardStep.Keys)
            {
                RecordKeyUp(e.KeyValue);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_step == WizardStep.Trigger && keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            UninstallHook();
            HotkeyManager.ResetForEditor();
            HotkeyManager.Suspended = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UninstallHook();
                _titleFont.Dispose();
                _subFont.Dispose();
                _comboFont.Dispose();
                _comboLabelFont.Dispose();
                _keyFont.Dispose();
                _keySmallFont.Dispose();
                _buttonFont.Dispose();
                _cardTitleFont.Dispose();
                _cardBodyFont.Dispose();
                _smallFont.Dispose();
                _minBox.Dispose();
                _maxBox.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
