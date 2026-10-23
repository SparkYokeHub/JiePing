using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class AboutPage : Control
    {
        private const string ContactEmail = "yjfk113@163.com";
        private readonly Bitmap _icon;
        private readonly Font _titleFont;
        private readonly Font _versionFont;
        private readonly Font _labelFont;
        private readonly Font _emailFont;
        private readonly Font _feedbackFont;
        private readonly Font _backFont;
        private readonly Font _footFont;
        private readonly TextBox _feedbackBox;
        private float _dpi = 1f;
        private Rectangle _backRect;
        private Rectangle _emailCardRect;
        private Rectangle _feedbackCardRect;
        private Rectangle _sendRect;
        private bool _hoverBack;
        private bool _hoverEmail;
        private bool _hoverSend;
        private bool _sending;
        private string _feedbackStatus = string.Empty;
        private bool _feedbackError;

        public event EventHandler BackRequested;

        public AboutPage()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = UiTheme.Background;
            DoubleBuffered = true;
            TabStop = true;

            _icon = LoadIcon();
            _titleFont = UiTheme.UiFont(26f, FontStyle.Bold);
            _versionFont = UiTheme.UiFont(10f, FontStyle.Regular);
            _labelFont = UiTheme.UiFont(8.5f, FontStyle.Regular);
            _emailFont = UiTheme.UiFont(12.5f, FontStyle.Bold);
            _feedbackFont = UiTheme.UiFont(9.2f, FontStyle.Regular);
            _backFont = UiTheme.UiFont(9.2f, FontStyle.Regular);
            _footFont = UiTheme.UiFont(8.5f, FontStyle.Regular);

            _feedbackBox = new TextBox();
            _feedbackBox.Multiline = true;
            _feedbackBox.ScrollBars = ScrollBars.Vertical;
            _feedbackBox.MaxLength = 2000;
            _feedbackBox.Font = UiTheme.UiFont(9f, FontStyle.Regular);
            _feedbackBox.BackColor = Color.FromArgb(17, 19, 24);
            _feedbackBox.ForeColor = UiTheme.Text;
            _feedbackBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_feedbackBox);
        }

        private static Bitmap LoadIcon()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_512.png"))
                {
                    if (stream != null)
                    {
                        using (Bitmap source = new Bitmap(stream))
                        {
                            return new Bitmap(source);
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
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
            int cardWidth = Math.Min((int)(560 * _dpi), Math.Max((int)(420 * _dpi), ClientSize.Width - (int)(120 * _dpi)));
            int cardY = Math.Min(ClientSize.Height - (int)(330 * _dpi), (int)(ClientSize.Height * 0.56f));
            _emailCardRect = new Rectangle((ClientSize.Width - cardWidth) / 2, cardY, cardWidth, (int)(84 * _dpi));
            _feedbackCardRect = new Rectangle(_emailCardRect.Left,
                _emailCardRect.Bottom + (int)(14 * _dpi), cardWidth, (int)(150 * _dpi));
            _sendRect = new Rectangle(_feedbackCardRect.Right - (int)(116 * _dpi),
                _feedbackCardRect.Top + (int)(34 * _dpi), (int)(96 * _dpi), (int)(34 * _dpi));
            _feedbackBox.Location = new Point(_feedbackCardRect.Left + (int)(18 * _dpi),
                _feedbackCardRect.Top + (int)(34 * _dpi));
            _feedbackBox.Size = new Size(
                Math.Max(120, _sendRect.Left - _feedbackBox.Left - (int)(12 * _dpi)),
                (int)(72 * _dpi));
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

            int iconSize = (int)(124 * _dpi);
            int iconX = (ClientSize.Width - iconSize) / 2;
            int iconY = Math.Max((int)(120 * _dpi), (int)(ClientSize.Height * 0.245f) - iconSize / 2);
            Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

            // Soft halo behind the app tile.
            for (int i = 3; i >= 1; i--)
            {
                Rectangle glow = Rectangle.Inflate(iconRect, i * 7, i * 7);
                using (GraphicsPath path = UiTheme.RoundedRect(glow, (32 + i * 7) * _dpi))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(8 + i * 3, 255, 104, 72)))
                {
                    g.FillPath(b, path);
                }
            }
            if (_icon != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_icon, iconRect);
            }
            else
            {
                using (GraphicsPath path = UiTheme.RoundedRect(iconRect, 28 * _dpi))
                using (SolidBrush b = new SolidBrush(UiTheme.Card))
                {
                    g.FillPath(b, path);
                }
            }

            string title = Loc.T("截一下", "JieYiXia");
            SizeF titleSize = g.MeasureString(title, _titleFont);
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(title, _titleFont, text,
                    (ClientSize.Width - titleSize.Width) / 2f,
                    iconRect.Bottom + 22 * _dpi);
            }

            string version = Loc.T("版本 ", "Version ") + Application.ProductVersion;
            SizeF versionSize = g.MeasureString(version, _versionFont);
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(version, _versionFont, muted,
                    (ClientSize.Width - versionSize.Width) / 2f,
                    iconRect.Bottom + 62 * _dpi);
            }

            using (Pen line = new Pen(Color.FromArgb(38, 42, 51)))
            {
                int lineY = _emailCardRect.Top - (int)(34 * _dpi);
                g.DrawLine(line, ClientSize.Width / 2 - (int)(210 * _dpi), lineY,
                    ClientSize.Width / 2 + (int)(210 * _dpi), lineY);
            }

            DrawEmailCard(g);
            DrawFeedbackCard(g);

            string foot = Loc.T("截一下  ·  本地截图工具  ·  无需联网",
                "JieYiXia  ·  Local screenshot tool  ·  Offline");
            SizeF footSize = g.MeasureString(foot, _footFont);
            using (SolidBrush faint = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(foot, _footFont, faint,
                    (ClientSize.Width - footSize.Width) / 2f,
                    ClientSize.Height - (int)(42 * _dpi));
            }
        }

        private void DrawBackButton(Graphics g)
        {
            if (_hoverBack)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(_backRect, 8f * _dpi))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(29, 32, 39)))
                {
                    g.FillPath(fill, path);
                }
            }
            using (SolidBrush text = new SolidBrush(_hoverBack ? UiTheme.Text : UiTheme.Muted))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Loc.T("←  图库", "←  Library"), _backFont, text, _backRect, sf);
            }
        }

        private void DrawEmailCard(Graphics g)
        {
            Color borderColor = _hoverEmail
                ? Color.FromArgb(120, 255, 126, 97)
                : Color.FromArgb(48, 52, 62);
            using (GraphicsPath path = UiTheme.RoundedRect(_emailCardRect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(_hoverEmail
                ? Color.FromArgb(32, 30, 30)
                : Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(borderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            float left = _emailCardRect.Left + 24 * _dpi;
            using (SolidBrush label = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("作者邮箱", "Author email"), _labelFont, label,
                    left, _emailCardRect.Top + 14 * _dpi);
            }
            using (SolidBrush email = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(ContactEmail, _emailFont, email, left, _emailCardRect.Top + 36 * _dpi);
            }

            SizeF emailSize = g.MeasureString(ContactEmail, _emailFont);
            float feedbackX = left + emailSize.Width + 18 * _dpi;
            using (SolidBrush accent = new SolidBrush(_hoverEmail ? UiTheme.AccentSoft : UiTheme.Accent))
            {
                g.DrawString(Loc.T("意见反馈、想要的功能", "Feedback & feature requests"), _feedbackFont, accent,
                    feedbackX, _emailCardRect.Top + 45 * _dpi);
            }
        }

        private void DrawFeedbackCard(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_feedbackCardRect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(48, 52, 62)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("意见反馈", "Feedback"), _emailFont, title,
                    _feedbackCardRect.Left + (int)(18 * _dpi),
                    _feedbackCardRect.Top + (int)(8 * _dpi));
            }
            using (SolidBrush red = new SolidBrush(Color.FromArgb(255, 112, 101)))
            {
                g.DrawString(Loc.T("暂不支持图片发送", "Images are not supported yet"), _labelFont, red,
                    _feedbackCardRect.Right - (int)(170 * _dpi),
                    _feedbackCardRect.Top + (int)(15 * _dpi));
            }

            DrawSendButton(g);

            string status = _sending
                ? Loc.T("正在发送…", "Sending...")
                : _feedbackStatus;
            if (!string.IsNullOrEmpty(status))
            {
                using (SolidBrush sb = new SolidBrush(_feedbackError
                    ? Color.FromArgb(255, 112, 101)
                    : Color.FromArgb(112, 211, 166)))
                {
                    g.DrawString(status, _labelFont, sb,
                        _feedbackCardRect.Left + (int)(18 * _dpi),
                        _feedbackCardRect.Top + (int)(108 * _dpi));
                }
            }
            using (SolidBrush hint = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("文字会直接发送到作者的更新后台。",
                        "Text is sent directly to the author's update service."),
                    _labelFont, hint,
                    _feedbackCardRect.Left + (int)(18 * _dpi),
                    _feedbackCardRect.Top + (int)(132 * _dpi));
            }
        }

        private void DrawSendButton(Graphics g)
        {
            Color fill = !_sending && _hoverSend
                ? Color.FromArgb(255, 136, 108)
                : (_sending ? Color.FromArgb(30, 33, 40) : UiTheme.Accent);
            Color textColor = _sending ? UiTheme.Faint : Color.FromArgb(38, 20, 15);
            using (GraphicsPath path = UiTheme.RoundedRect(_sendRect, 8f * _dpi))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(fill))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (SolidBrush tb = new SolidBrush(textColor))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Loc.T("发送", "Send"), _backFont, tb, _sendRect, sf);
            }
        }

        private void SendFeedback()
        {
            if (_sending)
            {
                return;
            }
            string text = (_feedbackBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                _feedbackStatus = Loc.T("请先输入反馈内容。", "Enter your feedback first.");
                _feedbackError = true;
                Invalidate();
                return;
            }
            _sending = true;
            _feedbackStatus = string.Empty;
            _feedbackError = false;
            Invalidate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    error = UpdateService.SendFeedback(text, string.Empty);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _sending = false;
                        if (string.IsNullOrEmpty(error))
                        {
                            _feedbackBox.Text = string.Empty;
                            _feedbackStatus = Loc.T("已发送，感谢你的反馈。", "Sent. Thank you for your feedback.");
                            _feedbackError = false;
                        }
                        else
                        {
                            _feedbackStatus = Loc.T("发送失败：", "Send failed: ") + error;
                            _feedbackError = true;
                        }
                        Invalidate();
                    }));
                }
                catch
                {
                }
            });
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool back = _backRect.Contains(e.Location);
            bool email = _emailCardRect.Contains(e.Location);
            bool send = _sendRect.Contains(e.Location);
            if (back != _hoverBack || email != _hoverEmail || send != _hoverSend)
            {
                _hoverBack = back;
                _hoverEmail = email;
                _hoverSend = send;
                Cursor = back || email || send ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverBack = false;
            _hoverEmail = false;
            _hoverSend = false;
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
            else if (_sendRect.Contains(e.Location))
            {
                SendFeedback();
            }
            else if (_emailCardRect.Contains(e.Location))
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        "mailto:" + ContactEmail + "?subject=" +
                        Uri.EscapeDataString(Loc.T("截一下 意见反馈 / 想要的功能",
                            "JieYiXia feedback / feature request")));
                }
                catch (Exception ex)
                {
                    CaptureStore.Log("mailto failed: " + ex.Message);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_icon != null)
                {
                    _icon.Dispose();
                }
                _titleFont.Dispose();
                _versionFont.Dispose();
                _labelFont.Dispose();
                _emailFont.Dispose();
                _feedbackFont.Dispose();
                _backFont.Dispose();
                _footFont.Dispose();
                _feedbackBox.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
