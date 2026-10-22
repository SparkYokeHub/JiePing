using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class SettingsPanel : Control
    {
        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _cardTitleFont;
        private readonly Font _bodyFont;
        private readonly Font _buttonFont;
        private readonly Font _pathFont;
        private readonly Font _noteFont;
        private readonly Font _hotkeyValueFont;

        private float _dpi = 1f;
        private string _path = string.Empty;
        private string _status = string.Empty;
        private bool _statusError;
        private HotkeyConfig _hotkey;
        private Rectangle _backRect;
        private Rectangle _changeRect;
        private Rectangle _openRect;
        private Rectangle _defaultRect;
        private Rectangle _cardRect;
        private Rectangle _pathRect;
        private Rectangle _hotkeyCardRect;
        private Rectangle _hotkeyButtonRect;
        private Rectangle _startupCardRect;
        private Rectangle _startupToggleRect;
        private Rectangle _closeCardRect;
        private Rectangle _closeTrayRect;
        private Rectangle _closeExitRect;
        private Rectangle _languageCardRect;
        private Rectangle _langZhRect;
        private Rectangle _langEnRect;
        private bool _startupEnabled;
        private CloseBehavior _closeBehavior;
        private int _hover = -1;

        public event EventHandler BackRequested;
        public event EventHandler FolderChanged;
        public event EventHandler HotkeyChanged;
        public event EventHandler LanguageChanged;

        public SettingsPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = UiTheme.Background;
            DoubleBuffered = true;
            TabStop = true;

            _titleFont = UiTheme.UiFont(21f, FontStyle.Bold);
            _subFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _cardTitleFont = UiTheme.UiFont(12.5f, FontStyle.Bold);
            _bodyFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _buttonFont = UiTheme.UiFont(9.2f, FontStyle.Bold);
            _pathFont = UiTheme.UiFont(9f, FontStyle.Regular);
            _noteFont = UiTheme.UiFont(8.8f, FontStyle.Regular);
            _hotkeyValueFont = UiTheme.UiFont(11.5f, FontStyle.Bold);
            _path = CaptureStore.ShotsDir;
            _hotkey = SettingsStore.GetHotkey();
            _startupEnabled = StartupManager.IsEnabled;
            _closeBehavior = SettingsStore.GetCloseBehavior();
        }

        public void RefreshPath()
        {
            _path = CaptureStore.ShotsDir;
            _hotkey = SettingsStore.GetHotkey();
            _startupEnabled = StartupManager.IsEnabled;
            _closeBehavior = SettingsStore.GetCloseBehavior();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics())
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            RecalcLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcLayout();
        }

        private void RecalcLayout()
        {
            _backRect = new Rectangle((int)(24 * _dpi), (int)(20 * _dpi), (int)(92 * _dpi), (int)(30 * _dpi));
            int left = (int)(32 * _dpi);
            int cardWidth = Math.Min((int)(760 * _dpi), Math.Max((int)(360 * _dpi), ClientSize.Width - left * 2));
            _cardRect = new Rectangle(left, (int)(150 * _dpi), cardWidth, (int)(214 * _dpi));
            _pathRect = new Rectangle(
                _cardRect.Left + (int)(24 * _dpi),
                _cardRect.Top + (int)(84 * _dpi),
                _cardRect.Width - (int)(48 * _dpi),
                (int)(44 * _dpi));
            int by = _cardRect.Top + (int)(146 * _dpi);
            _changeRect = new Rectangle(_cardRect.Left + (int)(24 * _dpi), by, (int)(112 * _dpi), (int)(38 * _dpi));
            _openRect = new Rectangle(_changeRect.Right + (int)(10 * _dpi), by, (int)(108 * _dpi), (int)(38 * _dpi));
            _defaultRect = new Rectangle(_openRect.Right + (int)(10 * _dpi), by, (int)(112 * _dpi), (int)(38 * _dpi));

            _hotkeyCardRect = new Rectangle(left, (int)(430 * _dpi), cardWidth, (int)(120 * _dpi));
            _hotkeyButtonRect = new Rectangle(
                _hotkeyCardRect.Right - (int)(124 * _dpi),
                _hotkeyCardRect.Top + (int)(46 * _dpi),
                (int)(100 * _dpi),
                (int)(38 * _dpi));

            _languageCardRect = new Rectangle(left, (int)(566 * _dpi), cardWidth, (int)(70 * _dpi));
            int langW = (int)(78 * _dpi);
            int langH = (int)(32 * _dpi);
            _langEnRect = new Rectangle(
                _languageCardRect.Right - (int)(20 * _dpi) - langW,
                _languageCardRect.Top + (int)(19 * _dpi),
                langW, langH);
            _langZhRect = new Rectangle(
                _langEnRect.Left - (int)(8 * _dpi) - langW,
                _langEnRect.Top,
                langW, langH);

            int bottomTop = (int)(650 * _dpi);
            int gap = (int)(20 * _dpi);
            int smallWidth = (cardWidth - gap) / 2;
            _startupCardRect = new Rectangle(left, bottomTop, smallWidth, (int)(150 * _dpi));
            _closeCardRect = new Rectangle(left + smallWidth + gap, bottomTop, smallWidth, (int)(150 * _dpi));
            _startupToggleRect = new Rectangle(
                _startupCardRect.Right - (int)(76 * _dpi),
                _startupCardRect.Top + (int)(20 * _dpi),
                (int)(52 * _dpi),
                (int)(28 * _dpi));

            int optionTop = _closeCardRect.Top + (int)(62 * _dpi);
            int optionWidth = (_closeCardRect.Width - (int)(46 * _dpi)) / 2;
            _closeTrayRect = new Rectangle(
                _closeCardRect.Left + (int)(18 * _dpi),
                optionTop,
                optionWidth,
                (int)(34 * _dpi));
            _closeExitRect = new Rectangle(
                _closeTrayRect.Right + (int)(10 * _dpi),
                optionTop,
                optionWidth,
                (int)(34 * _dpi));
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

            DrawBackButton(g);

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("设置", "Settings"), _titleFont, title,
                    (int)(30 * _dpi), (int)(58 * _dpi));
            }
            using (SolidBrush sub = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("截图、启动与关闭行为", "Capture, startup and close behavior"),
                    _subFont, sub, (int)(33 * _dpi), (int)(105 * _dpi));
            }

            using (GraphicsPath path = UiTheme.RoundedRect(_cardRect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(42, 46, 55)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("图片存放位置", "Screenshot folder"), _cardTitleFont, title,
                    _cardRect.Left + (int)(24 * _dpi), _cardRect.Top + (int)(22 * _dpi));
            }
            using (SolidBrush body = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("新截图将保存到这里；删除的截图会进入同目录下的 .recycle 回收站。",
                        "New screenshots are saved here. Deleted ones go to the .recycle folder."),
                    _bodyFont, body,
                    new RectangleF(
                        _cardRect.Left + (int)(24 * _dpi),
                        _cardRect.Top + (int)(50 * _dpi),
                        _cardRect.Width - (int)(48 * _dpi),
                        (int)(26 * _dpi)));
            }

            using (GraphicsPath path = UiTheme.RoundedRect(_pathRect, 8f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(14, 16, 21)))
            using (Pen border = new Pen(Color.FromArgb(48, 52, 62)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush iconBrush = new SolidBrush(UiTheme.Faint))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisPath;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(_path, _pathFont, iconBrush,
                    new RectangleF(
                        _pathRect.Left + (int)(14 * _dpi),
                        _pathRect.Top,
                        _pathRect.Width - (int)(28 * _dpi),
                        _pathRect.Height),
                    sf);
            }

            DrawButton(g, _changeRect, Loc.T("更改位置", "Change"), 0, true);
            DrawButton(g, _openRect, Loc.T("打开文件夹", "Open Folder"), 1, false);
            DrawButton(g, _defaultRect, Loc.T("恢复默认", "Reset"), 2, false);

            string note = Loc.T("更改位置时，现有截图和回收站会一起迁移到新文件夹。",
                "Changing the folder moves existing screenshots and the recycle bin.");
            using (SolidBrush noteBrush = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(note, _noteFont, noteBrush,
                    _cardRect.Left + (int)(2 * _dpi),
                    _cardRect.Bottom + (int)(14 * _dpi));
            }

            if (!string.IsNullOrEmpty(_status))
            {
                using (SolidBrush statusBrush = new SolidBrush(_statusError
                    ? Color.FromArgb(255, 115, 105)
                    : Color.FromArgb(112, 211, 166)))
                {
                    g.DrawString(_status, _noteFont, statusBrush,
                        _cardRect.Left + (int)(2 * _dpi),
                        _cardRect.Bottom + (int)(38 * _dpi));
                }
            }

            DrawHotkeyCard(g);
            DrawLanguageCard(g);
            DrawStartupCard(g);
            DrawCloseCard(g);
        }

        private void DrawLanguageCard(Graphics g)
        {
            DrawSmallCard(g, _languageCardRect);
            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("语言", "Language"), _cardTitleFont, title,
                    _languageCardRect.Left + (int)(20 * _dpi),
                    _languageCardRect.Top + (int)(12 * _dpi));
            }
            using (SolidBrush body = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("界面语言", "Interface language"),
                    _noteFont, body,
                    _languageCardRect.Left + (int)(20 * _dpi),
                    _languageCardRect.Top + (int)(39 * _dpi));
            }
            DrawLanguageButton(g, _langZhRect, "中文", !Loc.IsEnglish, 8);
            DrawLanguageButton(g, _langEnRect, "English", Loc.IsEnglish, 9);
        }

        private void DrawLanguageButton(Graphics g, Rectangle rect, string text, bool selected, int index)
        {
            bool hover = _hover == index;
            Color fill = selected
                ? Color.FromArgb(52, 31, 27)
                : (hover ? Color.FromArgb(38, 42, 51) : Color.FromArgb(25, 28, 35));
            Color border = selected ? UiTheme.Accent : Color.FromArgb(48, 52, 62);
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 8f * _dpi))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border, selected ? 1.4f : 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (SolidBrush tb = new SolidBrush(selected ? UiTheme.AccentSoft : UiTheme.Text))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, _buttonFont, tb, rect, sf);
            }
        }

        private void DrawStartupCard(Graphics g)
        {
            DrawSmallCard(g, _startupCardRect);
            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("开机自启动", "Start with Windows"), _cardTitleFont, title,
                    _startupCardRect.Left + (int)(18 * _dpi),
                    _startupCardRect.Top + (int)(18 * _dpi));
            }
            using (SolidBrush body = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("登录 Windows 后自动在托盘运行。",
                        "Stay in the tray after you sign in to Windows."), _bodyFont, body,
                    _startupCardRect.Left + (int)(18 * _dpi),
                    _startupCardRect.Top + (int)(48 * _dpi));
                g.DrawString(_startupEnabled
                        ? Loc.T("已开启 · 开机后自动驻留托盘", "On · starts in the tray")
                        : Loc.T("已关闭 · 需要手动打开", "Off · open manually"),
                    _noteFont, body,
                    _startupCardRect.Left + (int)(18 * _dpi),
                    _startupCardRect.Top + (int)(78 * _dpi));
                g.DrawString(Loc.T("启动会延迟约 2.6 秒初始化，不拖慢开机。",
                        "Startup is delayed by about 2.6 s so boot stays fast."),
                    _noteFont, body,
                    _startupCardRect.Left + (int)(18 * _dpi),
                    _startupCardRect.Top + (int)(110 * _dpi));
            }

            bool hover = _hover == 5;
            Color track = _startupEnabled
                ? UiTheme.Accent
                : Color.FromArgb(hover ? 62 : 52, 56, 66);
            using (GraphicsPath path = UiTheme.RoundedRect(_startupToggleRect, 14f * _dpi))
            using (SolidBrush fill = new SolidBrush(track))
            {
                g.FillPath(fill, path);
            }
            int knobSize = (int)(22 * _dpi);
            int knobX = _startupEnabled
                ? _startupToggleRect.Right - knobSize - (int)(3 * _dpi)
                : _startupToggleRect.Left + (int)(3 * _dpi);
            Rectangle knob = new Rectangle(knobX,
                _startupToggleRect.Top + (int)(3 * _dpi), knobSize, knobSize);
            using (SolidBrush kb = new SolidBrush(Color.FromArgb(246, 242, 236)))
            {
                g.FillEllipse(kb, knob);
            }
        }

        private void DrawCloseCard(Graphics g)
        {
            DrawSmallCard(g, _closeCardRect);
            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("关闭窗口时", "When closing the window"), _cardTitleFont, title,
                    _closeCardRect.Left + (int)(18 * _dpi),
                    _closeCardRect.Top + (int)(18 * _dpi));
            }
            using (SolidBrush body = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("点击右上角关闭按钮后的行为", "Behavior when you click the close button"),
                    _noteFont, body,
                    _closeCardRect.Left + (int)(18 * _dpi),
                    _closeCardRect.Top + (int)(44 * _dpi));
            }

            DrawCloseOption(g, _closeTrayRect, Loc.T("隐藏到托盘", "Hide to tray"),
                _closeBehavior == CloseBehavior.Tray, 6);
            DrawCloseOption(g, _closeExitRect, Loc.T("直接退出", "Exit"),
                _closeBehavior == CloseBehavior.Exit, 7);

            using (SolidBrush note = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("隐藏到托盘：快捷键仍可用，可继续截图。",
                        "Hide to tray: the shortcut keeps working."), _noteFont, note,
                    _closeCardRect.Left + (int)(18 * _dpi),
                    _closeCardRect.Top + (int)(108 * _dpi));
                g.DrawString(Loc.T("直接退出：程序停止，截图快捷键失效。",
                        "Exit: JiePing stops and the shortcut no longer works."), _noteFont, note,
                    _closeCardRect.Left + (int)(18 * _dpi),
                    _closeCardRect.Top + (int)(128 * _dpi));
            }
        }

        private void DrawCloseOption(Graphics g, Rectangle rect, string text, bool selected, int index)
        {
            bool hover = _hover == index;
            Color fill = selected
                ? Color.FromArgb(52, 31, 27)
                : (hover ? Color.FromArgb(38, 42, 51) : Color.FromArgb(25, 28, 35));
            Color border = selected ? UiTheme.Accent : Color.FromArgb(48, 52, 62);
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 8f * _dpi))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border, selected ? 1.4f : 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (SolidBrush tb = new SolidBrush(selected ? UiTheme.AccentSoft : UiTheme.Text))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, _buttonFont, tb, rect, sf);
            }
        }

        private void DrawSmallCard(Graphics g, Rectangle rect)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(42, 46, 55)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        private void DrawHotkeyCard(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_hotkeyCardRect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(42, 46, 55)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("自定义快捷键", "Custom shortcut"), _cardTitleFont, title,
                    _hotkeyCardRect.Left + (int)(24 * _dpi),
                    _hotkeyCardRect.Top + (int)(18 * _dpi));
            }
            using (SolidBrush desc = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("设置截图组合键、点击/长按触发方式与延迟区间。",
                        "Set the shortcut, tap/hold trigger and delay range."), _bodyFont, desc,
                    _hotkeyCardRect.Left + (int)(24 * _dpi),
                    _hotkeyCardRect.Top + (int)(44 * _dpi));
            }
            using (SolidBrush value = new SolidBrush(UiTheme.AccentSoft))
            {
                g.DrawString(_hotkey == null ? Loc.T("未设置", "Not set") : _hotkey.DisplayText,
                    _hotkeyValueFont, value,
                    _hotkeyCardRect.Left + (int)(24 * _dpi),
                    _hotkeyCardRect.Top + (int)(68 * _dpi));
            }
            using (SolidBrush summary = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(_hotkey == null ? string.Empty : _hotkey.Summary,
                    _noteFont, summary,
                    _hotkeyCardRect.Left + (int)(24 * _dpi),
                    _hotkeyCardRect.Top + (int)(94 * _dpi));
            }

            DrawButton(g, _hotkeyButtonRect, Loc.T("自定义", "Customize"), 4, false);
        }

        private void DrawBackButton(Graphics g)
        {
            if (_hover == 3)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(_backRect, 8f * _dpi))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(28, 31, 38)))
                {
                    g.FillPath(fill, path);
                }
            }
            using (SolidBrush text = new SolidBrush(_hover == 3 ? UiTheme.Text : UiTheme.Muted))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Loc.T("←  图库", "←  Library"), _bodyFont, text, _backRect, sf);
            }
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, int index, bool primary)
        {
            bool hover = _hover == index;
            Color fill;
            Color border;
            Color textColor;
            if (primary)
            {
                fill = hover ? Color.FromArgb(255, 126, 96) : UiTheme.Accent;
                border = fill;
                textColor = Color.FromArgb(33, 18, 13);
            }
            else
            {
                fill = hover ? Color.FromArgb(42, 45, 54) : Color.FromArgb(29, 32, 39);
                border = Color.FromArgb(52, 56, 66);
                textColor = UiTheme.Text;
            }

            using (GraphicsPath path = UiTheme.RoundedRect(rect, 8f * _dpi))
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
            int hover = -1;
            if (_backRect.Contains(e.Location))
            {
                hover = 3;
            }
            else if (_changeRect.Contains(e.Location))
            {
                hover = 0;
            }
            else if (_openRect.Contains(e.Location))
            {
                hover = 1;
            }
            else if (_defaultRect.Contains(e.Location))
            {
                hover = 2;
            }
            else if (_hotkeyButtonRect.Contains(e.Location))
            {
                hover = 4;
            }
            else if (_startupToggleRect.Contains(e.Location))
            {
                hover = 5;
            }
            else if (_closeTrayRect.Contains(e.Location))
            {
                hover = 6;
            }
            else if (_closeExitRect.Contains(e.Location))
            {
                hover = 7;
            }
            else if (_langZhRect.Contains(e.Location))
            {
                hover = 8;
            }
            else if (_langEnRect.Contains(e.Location))
            {
                hover = 9;
            }
            if (hover != _hover)
            {
                _hover = hover;
                Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
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
            if (_backRect.Contains(e.Location))
            {
                EventHandler handler = BackRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            else if (_changeRect.Contains(e.Location))
            {
                ChooseFolder();
            }
            else if (_openRect.Contains(e.Location))
            {
                CaptureStore.OpenFolder();
            }
            else if (_defaultRect.Contains(e.Location))
            {
                ConfirmDefault();
            }
            else if (_hotkeyButtonRect.Contains(e.Location))
            {
                OpenHotkeyWizard();
            }
            else if (_startupToggleRect.Contains(e.Location))
            {
                ToggleStartup();
            }
            else if (_closeTrayRect.Contains(e.Location))
            {
                SetCloseBehavior(CloseBehavior.Tray);
            }
            else if (_closeExitRect.Contains(e.Location))
            {
                SetCloseBehavior(CloseBehavior.Exit);
            }
            else if (_langZhRect.Contains(e.Location))
            {
                SetLanguage(AppLanguage.Chinese);
            }
            else if (_langEnRect.Contains(e.Location))
            {
                SetLanguage(AppLanguage.English);
            }
        }

        private void SetLanguage(AppLanguage language)
        {
            if ((language == AppLanguage.English) == Loc.IsEnglish)
            {
                return;
            }
            bool english = language == AppLanguage.English;
            string error;
            if (!SettingsStore.SaveLanguage(language, out error))
            {
                _status = string.IsNullOrEmpty(error)
                    ? Loc.T("语言设置保存失败。", "Failed to save the language setting.")
                    : error;
                _statusError = true;
                Invalidate();
                return;
            }
            Loc.IsEnglish = english;
            _status = english ? "Language switched to English." : "已切换为中文。";
            _statusError = false;
            EventHandler handler = LanguageChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
            Invalidate();
        }

        private void ToggleStartup()
        {
            bool target = !_startupEnabled;
            string error;
            if (!StartupManager.TrySetEnabled(target, out error))
            {
                _status = string.IsNullOrEmpty(error)
                    ? Loc.T("开机自启动设置失败。", "Failed to change the startup setting.")
                    : error;
                _statusError = true;
                Invalidate();
                return;
            }
            _startupEnabled = StartupManager.IsEnabled;
            _status = _startupEnabled
                ? Loc.T("已开启开机自启动。", "Start with Windows is on.")
                : Loc.T("已关闭开机自启动。", "Start with Windows is off.");
            _statusError = false;
            Invalidate();
        }

        private void SetCloseBehavior(CloseBehavior behavior)
        {
            if (_closeBehavior == behavior)
            {
                return;
            }
            string error;
            if (!SettingsStore.SaveCloseBehavior(behavior, out error))
            {
                _status = string.IsNullOrEmpty(error)
                    ? Loc.T("关闭行为保存失败。", "Failed to save the close behavior.")
                    : error;
                _statusError = true;
                Invalidate();
                return;
            }
            _closeBehavior = behavior;
            _status = behavior == CloseBehavior.Tray
                ? Loc.T("关闭窗口后将隐藏到托盘，截图快捷键继续可用。",
                    "The window will hide to the tray; the shortcut stays available.")
                : Loc.T("关闭窗口后将直接退出，截图快捷键会停止。",
                    "The window will close; the shortcut will stop working.");
            _statusError = false;
            Invalidate();
        }

        private void OpenHotkeyWizard()
        {
            Form owner = FindForm();
            using (ShortcutWizard wizard = new ShortcutWizard(_hotkey, owner == null ? null : owner.Icon))
            {
                if (wizard.ShowDialog(owner) != DialogResult.OK || wizard.SelectedConfig == null)
                {
                    return;
                }
                string error;
                if (!SettingsStore.SaveHotkey(wizard.SelectedConfig, out error))
                {
                    _status = string.IsNullOrEmpty(error)
                        ? Loc.T("快捷键保存失败。", "Failed to save the shortcut.")
                        : error;
                    _statusError = true;
                    Invalidate();
                    return;
                }
                _hotkey = wizard.SelectedConfig;
                _status = Loc.T("快捷键已更新：", "Shortcut updated: ") + _hotkey.DisplayText;
                _statusError = false;
                EventHandler handler = HotkeyChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
                Invalidate();
            }
        }

        private void ChooseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = Loc.T("选择截图存放文件夹", "Choose a screenshot folder");
                dialog.ShowNewFolderButton = true;
                try
                {
                    if (System.IO.Directory.Exists(_path))
                    {
                        dialog.SelectedPath = _path;
                    }
                }
                catch
                {
                }
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    ChangeTo(dialog.SelectedPath);
                }
            }
        }

        private void ConfirmDefault()
        {
            using (ConfirmDialog dialog = new ConfirmDialog(
                Loc.T("恢复默认位置？", "Reset to the default folder?"),
                Loc.T("现有截图和回收站会迁移回默认截图文件夹。",
                    "Existing screenshots and the recycle bin will move back to the default folder."),
                Loc.T("恢复", "Reset")))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || !dialog.Confirmed)
                {
                    return;
                }
            }
            ChangeTo(SettingsStore.DefaultImageFolder);
        }

        private void ChangeTo(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            try
            {
                if (string.Equals(
                    System.IO.Path.GetFullPath(folder),
                    CaptureStore.ShotsDir,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _status = Loc.T("当前已经是这个位置。", "This is already the current folder.");
                    _statusError = false;
                    Invalidate();
                    return;
                }
            }
            catch
            {
            }

            UseWaitCursor = true;
            Cursor = Cursors.WaitCursor;
            Refresh();
            string error;
            bool ok = CaptureStore.ChangeImageFolder(folder, out error);
            UseWaitCursor = false;
            Cursor = Cursors.Default;

            if (ok)
            {
                _path = CaptureStore.ShotsDir;
                _status = Loc.T("图片存放位置已更新，现有截图已迁移。",
                    "Screenshot folder updated and existing files moved.");
                _statusError = false;
                EventHandler handler = FolderChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            else
            {
                _status = string.IsNullOrEmpty(error)
                    ? Loc.T("更改位置失败。", "Failed to change the folder.")
                    : error;
                _statusError = true;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subFont.Dispose();
                _cardTitleFont.Dispose();
                _bodyFont.Dispose();
                _buttonFont.Dispose();
                _pathFont.Dispose();
                _noteFont.Dispose();
                _hotkeyValueFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
