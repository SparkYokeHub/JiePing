using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class RecyclePage : UserControl
    {
        private readonly GalleryPanel _grid;
        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _backFont;
        private float _dpi = 1f;
        private int _headerHeight = 92;
        private Rectangle _backRect;
        private bool _hoverBack;

        public event EventHandler BackRequested;

        public RecyclePage()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Background;
            DoubleBuffered = true;

            _titleFont = UiTheme.UiFont(19f, FontStyle.Bold);
            _subFont = UiTheme.UiFont(9f, FontStyle.Regular);
            _backFont = UiTheme.UiFont(9.2f, FontStyle.Regular);

            _grid = new GalleryPanel(GalleryMode.Recycle);
            _grid.Dock = DockStyle.Fill;
            _grid.ItemsChanged += delegate { Invalidate(); };
            Controls.Add(_grid);
            Padding = new Padding(0, _headerHeight, 0, 0);
        }

        public GalleryPanel Grid
        {
            get { return _grid; }
        }

        public void RefreshData()
        {
            _grid.Reload(true);
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics())
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            _headerHeight = (int)(92 * _dpi);
            Padding = new Padding(0, _headerHeight, 0, 0);
            RecalcBack();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcBack();
        }

        private void RecalcBack()
        {
            _backRect = new Rectangle(
                Math.Max(0, Width - (int)(138 * _dpi)),
                (int)(24 * _dpi),
                (int)(112 * _dpi),
                (int)(32 * _dpi));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(UiTheme.Background))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            using (SolidBrush headerBg = new SolidBrush(Color.FromArgb(13, 15, 19)))
            {
                g.FillRectangle(headerBg, 0, 0, Width, _headerHeight);
            }
            using (SolidBrush line = new SolidBrush(Color.FromArgb(35, 38, 46)))
            {
                g.FillRectangle(line, 0, _headerHeight - 1, Width, 1);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("回收站", "Recycle Bin"), _titleFont, title,
                    (int)(26 * _dpi), (int)(16 * _dpi));
            }
            string sub = Loc.Format("{0} 张  ·  右下角删除会从计算机永久移除",
                "{0}  ·  deleting from the bottom-right removes files permanently", _grid.Count);
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(sub, _subFont, muted, (int)(28 * _dpi), (int)(52 * _dpi));
            }

            if (_hoverBack)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(_backRect, 8f * _dpi))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(30, 33, 41)))
                {
                    g.FillPath(fill, path);
                }
            }
            using (SolidBrush text = new SolidBrush(_hoverBack ? UiTheme.Text : UiTheme.Muted))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Loc.T("←  返回图库", "←  Back to Library"), _backFont, text, _backRect, sf);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = _backRect.Contains(e.Location);
            if (hover != _hoverBack)
            {
                _hoverBack = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverBack)
            {
                _hoverBack = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _backRect.Contains(e.Location))
            {
                EventHandler handler = BackRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subFont.Dispose();
                _backFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
