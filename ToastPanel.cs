using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class ToastPanel : Control
    {
        private readonly Font _font;
        private readonly Timer _timer;
        private string _text = string.Empty;
        private bool _error;

        public ToastPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            TabStop = false;
            Visible = false;
            Size = new Size(360, 42);
            _font = UiTheme.UiFont(9.2f, FontStyle.Regular);
            _timer = new Timer();
            _timer.Interval = 2400;
            _timer.Tick += delegate
            {
                _timer.Stop();
                Visible = false;
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT: clicks pass through.
                return cp;
            }
        }

        public void ShowMessage(string text, bool error)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            _text = text;
            _error = error;
            _timer.Stop();
            Visible = true;
            BringToFront();
            Invalidate();
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UiTheme.RoundedRect(rect, Height / 2f))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(242, 26, 29, 36)))
            using (Pen border = new Pen(Color.FromArgb(70, 76, 89)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Color dot = _error ? Color.FromArgb(255, 105, 95) : Color.FromArgb(112, 211, 166);
            using (SolidBrush dotBrush = new SolidBrush(dot))
            {
                g.FillEllipse(dotBrush, 16, Height / 2 - 3, 6, 6);
            }
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(_text, _font, text,
                    new RectangleF(32, 0, Width - 46, Height), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
