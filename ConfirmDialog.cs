using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class ConfirmDialog : Form
    {
        private readonly string _title;
        private readonly string _message;
        private readonly string _confirmText;
        private Rectangle _cancelRect;
        private Rectangle _confirmRect;
        private bool _cancelHover;
        private bool _confirmHover;

        public bool Confirmed { get; private set; }

        public ConfirmDialog(string title, string message, string confirmText)
        {
            _title = title;
            _message = message;
            _confirmText = confirmText;

            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(410, 184);
            BackColor = UiTheme.Panel;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _cancelRect = new Rectangle(ClientSize.Width - 214, ClientSize.Height - 60, 92, 36);
            _confirmRect = new Rectangle(ClientSize.Width - 110, ClientSize.Height - 60, 92, 36);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.EnableDarkTitleBar(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush bg = new SolidBrush(UiTheme.Panel))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            using (Pen border = new Pen(Color.FromArgb(48, 51, 61)))
            {
                g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            using (Font titleFont = UiTheme.UiFont(13.5f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(_title, titleFont, text, new PointF(26, 26));
            }
            using (Font bodyFont = UiTheme.UiFont(9.5f, FontStyle.Regular))
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(_message, bodyFont, muted, new RectangleF(26, 65, ClientSize.Width - 52, 56));
            }

            DrawButton(g, _cancelRect, Loc.T("取消", "Cancel"), false, _cancelHover);
            DrawButton(g, _confirmRect, _confirmText, true, _confirmHover);
        }

        private void DrawButton(Graphics g, Rectangle r, string text, bool primary, bool hover)
        {
            Color fill;
            Color border;
            Color textColor;
            if (primary)
            {
                fill = hover ? Color.FromArgb(255, 121, 92) : UiTheme.Accent;
                border = fill;
                textColor = Color.FromArgb(26, 15, 12);
            }
            else
            {
                fill = hover ? Color.FromArgb(40, 43, 52) : Color.FromArgb(29, 32, 39);
                border = Color.FromArgb(52, 56, 66);
                textColor = UiTheme.Text;
            }

            using (GraphicsPath path = UiTheme.RoundedRect(r, 8f))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (Font f = UiTheme.UiFont(9.5f, FontStyle.Bold))
            using (SolidBrush tb = new SolidBrush(textColor))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, f, tb, r, sf);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool cancel = _cancelRect.Contains(e.Location);
            bool confirm = _confirmRect.Contains(e.Location);
            if (cancel != _cancelHover || confirm != _confirmHover)
            {
                _cancelHover = cancel;
                _confirmHover = confirm;
                Cursor = cancel || confirm ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _cancelHover = false;
            _confirmHover = false;
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
            if (_cancelRect.Contains(e.Location))
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (_confirmRect.Contains(e.Location))
            {
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
