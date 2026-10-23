using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class SidebarPanel : Control
    {
        private readonly Bitmap _brandIcon;
        private readonly Font _brandFont;
        private readonly Font _brandSubFont;
        private readonly Font _navFont;
        private readonly Font _badgeFont;
        private readonly Font _keyFont;
        private readonly Font _tinyFont;

        private float _dpi = 1f;
        private AppView _selected = AppView.Gallery;
        private int _recycleCount;
        private string _hotkeyText = "Ctrl + Alt";
        private Rectangle _brandRect;
        private Rectangle _settingsRect;
        private Rectangle _recycleRect;
        private Rectangle _rewardRect;
        private Rectangle _updateRect;
        private Rectangle _aboutRect;
        private Rectangle _toggleRect;
        private bool _collapsed;
        private bool _hoverBrand;
        private bool _hoverSettings;
        private bool _hoverRecycle;
        private bool _hoverReward;
        private bool _hoverUpdate;
        private bool _hoverAbout;
        private bool _hoverToggle;

        public event EventHandler GalleryRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler RecycleRequested;
        public event EventHandler RewardRequested;
        public event EventHandler UpdateRequested;
        public event EventHandler AboutRequested;
        public event EventHandler ToggleRequested;

        public SidebarPanel(Icon appIcon)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(14, 16, 20);
            DoubleBuffered = true;
            TabStop = false;

            _brandIcon = LoadBrandBitmap(appIcon);

            _brandFont = UiTheme.UiFont(13.5f, FontStyle.Bold);
            _brandSubFont = UiTheme.UiFont(8.5f, FontStyle.Regular);
            _navFont = UiTheme.UiFont(10f, FontStyle.Regular);
            _badgeFont = UiTheme.UiFont(8f, FontStyle.Bold);
            _keyFont = UiTheme.UiFont(8.5f, FontStyle.Regular);
            _tinyFont = UiTheme.UiFont(8f, FontStyle.Regular);
        }

        private static Bitmap LoadBrandBitmap(Icon appIcon)
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
            try
            {
                if (appIcon != null)
                {
                    using (Icon sized = new Icon(appIcon, 64, 64))
                    {
                        return sized.ToBitmap();
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public AppView SelectedView
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (_collapsed != value)
                {
                    _collapsed = value;
                    RecalcLayout();
                    Invalidate();
                }
            }
        }

        public int RecycleCount
        {
            get { return _recycleCount; }
            set
            {
                int next = Math.Max(0, value);
                if (_recycleCount != next)
                {
                    _recycleCount = next;
                    Invalidate(_recycleRect.IsEmpty ? ClientRectangle : _recycleRect);
                }
            }
        }

        public string HotkeyText
        {
            get { return _hotkeyText; }
            set
            {
                string next = string.IsNullOrEmpty(value) ? Loc.T("未设置", "Not set") : value;
                if (!string.Equals(_hotkeyText, next, StringComparison.Ordinal))
                {
                    _hotkeyText = next;
                    Invalidate();
                }
            }
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
            int left = (int)(12 * _dpi);
            int width = Math.Max(40, Width - left * 2);
            int top = (int)(16 * _dpi);
            if (_collapsed)
            {
                _brandRect = new Rectangle(left, (int)(10 * _dpi), width, (int)(40 * _dpi));
                _toggleRect = new Rectangle((Width - (int)(26 * _dpi)) / 2,
                    (int)(56 * _dpi), (int)(26 * _dpi), (int)(26 * _dpi));
            }
            else
            {
                _brandRect = new Rectangle(left, top, width, (int)(58 * _dpi));
                _toggleRect = new Rectangle(Width - (int)(38 * _dpi), (int)(26 * _dpi),
                    (int)(26 * _dpi), (int)(26 * _dpi));
            }
            int navTop = (int)(96 * _dpi);
            int itemHeight = (int)(42 * _dpi);
            _settingsRect = new Rectangle(left, navTop, width, itemHeight);
            _recycleRect = new Rectangle(left, navTop + itemHeight + (int)(6 * _dpi), width, itemHeight);
            _rewardRect = new Rectangle(left, _recycleRect.Bottom + (int)(6 * _dpi), width, itemHeight);
            _updateRect = new Rectangle(left, _rewardRect.Bottom + (int)(6 * _dpi), width, itemHeight);
            _aboutRect = new Rectangle(left, _updateRect.Bottom + (int)(6 * _dpi), width, itemHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(14, 16, 20)))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            using (SolidBrush line = new SolidBrush(Color.FromArgb(32, 35, 43)))
            {
                g.FillRectangle(line, Width - 1, 0, 1, Height);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            DrawBrand(g);
            DrawNavItem(g, _settingsRect, Loc.T("设置", "Settings"), _selected == AppView.Settings, _hoverSettings, false, false, false, false, 0);
            DrawNavItem(g, _recycleRect, Loc.T("回收站", "Recycle Bin"), _selected == AppView.Recycle, _hoverRecycle, true, false, false, false, _recycleCount);
            DrawNavItem(g, _rewardRect, Loc.T("赞赏", "Support"), _selected == AppView.Reward, _hoverReward, false, false, true, false, 0);
            DrawNavItem(g, _updateRect, Loc.T("软件升级", "Update"), _selected == AppView.Update, _hoverUpdate, false, false, false, true, 0);
            DrawNavItem(g, _aboutRect, Loc.T("关于", "About"), _selected == AppView.About, _hoverAbout, false, true, false, false, 0);
            if (!_collapsed)
            {
                DrawFooter(g);
            }
            DrawToggle(g);
        }

        private void DrawBrand(Graphics g)
        {
            if (_hoverBrand || _selected == AppView.Gallery)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(_brandRect, 10f * _dpi))
                using (SolidBrush fill = new SolidBrush(_selected == AppView.Gallery
                    ? Color.FromArgb(30, 33, 40)
                    : Color.FromArgb(21, 24, 30)))
                {
                    g.FillPath(fill, path);
                }
            }

            int iconSize = (int)((_collapsed ? 28 : 32) * _dpi);
            int iconLeft = _collapsed
                ? _brandRect.Left + (_brandRect.Width - iconSize) / 2
                : _brandRect.Left + (int)(8 * _dpi);
            Rectangle iconRect = new Rectangle(
                iconLeft,
                _brandRect.Top + (_brandRect.Height - iconSize) / 2,
                iconSize,
                iconSize);
            if (_brandIcon != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(_brandIcon, iconRect);
            }
            if (_collapsed)
            {
                return;
            }

            float textX = iconRect.Right + 10 * _dpi;
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("截一下", "JieYiXia"), _brandFont, text, textX, _brandRect.Top + 8 * _dpi);
            }
            string subtitle = _selected == AppView.Gallery ? Loc.T("图库", "Library") : _hotkeyText;
            using (SolidBrush muted = new SolidBrush(_selected == AppView.Gallery ? UiTheme.Accent : UiTheme.Faint))
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                RectangleF subtitleRect = new RectangleF(
                    textX + 1,
                    _brandRect.Top + 34 * _dpi,
                    Math.Max(20, _brandRect.Right - textX - 10 * _dpi),
                    22 * _dpi);
                g.DrawString(subtitle, _brandSubFont, muted, subtitleRect, sf);
            }
        }

        private void DrawNavItem(Graphics g, Rectangle rect, string text, bool selected, bool hover,
            bool trash, bool info, bool heart, bool update, int badge)
        {
            if (selected || hover)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(rect, 9f * _dpi))
                using (SolidBrush fill = new SolidBrush(selected
                    ? Color.FromArgb(33, 36, 44)
                    : Color.FromArgb(22, 25, 31)))
                {
                    g.FillPath(fill, path);
                }
            }
            if (selected)
            {
                Rectangle accent = new Rectangle(
                    rect.Left + (int)(1 * _dpi),
                    rect.Top + (int)(10 * _dpi),
                    (int)(3 * _dpi),
                    rect.Height - (int)(20 * _dpi));
                using (GraphicsPath path = UiTheme.RoundedRect(accent, 1.5f * _dpi))
                using (SolidBrush fill = new SolidBrush(UiTheme.Accent))
                {
                    g.FillPath(fill, path);
                }
            }

            int iconSize = (int)(18 * _dpi);
            int iconLeft = _collapsed
                ? rect.Left + (rect.Width - iconSize) / 2
                : rect.Left + (int)(16 * _dpi);
            Rectangle iconRect = new Rectangle(
                iconLeft,
                rect.Top + (rect.Height - iconSize) / 2,
                iconSize,
                iconSize);
            Color iconColor = selected || hover ? UiTheme.Text : Color.FromArgb(174, 179, 190);
            if (trash)
            {
                DrawTrashIcon(g, iconRect, iconColor);
            }
            else if (info)
            {
                DrawInfoIcon(g, iconRect, iconColor);
            }
            else if (heart)
            {
                DrawHeartIcon(g, iconRect, iconColor);
            }
            else if (update)
            {
                DrawUpdateIcon(g, iconRect, iconColor);
            }
            else
            {
                DrawGearIcon(g, iconRect, iconColor);
            }

            if (!_collapsed)
            {
                using (SolidBrush textBrush = new SolidBrush(selected || hover ? UiTheme.Text : Color.FromArgb(198, 202, 210)))
                {
                    g.DrawString(text, _navFont, textBrush,
                        rect.Left + (int)(46 * _dpi),
                        rect.Top + (rect.Height - _navFont.GetHeight(g)) / 2f);
                }
            }

            if (badge > 0 && !_collapsed)
            {
                string label = badge > 99 ? "99+" : badge.ToString();
                SizeF measured = g.MeasureString(label, _badgeFont);
                int bw = Math.Max((int)(22 * _dpi), (int)measured.Width + (int)(10 * _dpi));
                int bh = (int)(18 * _dpi);
                Rectangle pill = new Rectangle(
                    rect.Right - bw - (int)(10 * _dpi),
                    rect.Top + (rect.Height - bh) / 2,
                    bw,
                    bh);
                using (GraphicsPath path = UiTheme.RoundedRect(pill, bh / 2f))
                using (SolidBrush fill = new SolidBrush(selected ? UiTheme.Accent : Color.FromArgb(53, 57, 67)))
                {
                    g.FillPath(fill, path);
                }
                using (SolidBrush tb = new SolidBrush(selected ? Color.FromArgb(36, 20, 15) : UiTheme.Text))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(label, _badgeFont, tb, pill, sf);
                }
            }
        }

        private void DrawGearIcon(Graphics g, Rectangle r, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            float outer = r.Width * 0.42f;
            float inner = r.Width * 0.17f;
            using (Pen pen = new Pen(color, 1.5f * _dpi))
            {
                g.DrawEllipse(pen, cx - outer * 0.62f, cy - outer * 0.62f, outer * 1.24f, outer * 1.24f);
                g.DrawEllipse(pen, cx - inner, cy - inner, inner * 2f, inner * 2f);
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI * 2 * i / 8.0;
                    float x1 = cx + (float)Math.Cos(angle) * outer * 0.72f;
                    float y1 = cy + (float)Math.Sin(angle) * outer * 0.72f;
                    float x2 = cx + (float)Math.Cos(angle) * outer;
                    float y2 = cy + (float)Math.Sin(angle) * outer;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        private void DrawTrashIcon(Graphics g, Rectangle r, Color color)
        {
            using (Pen pen = new Pen(color, 1.5f * _dpi))
            {
                float left = r.Left + r.Width * 0.22f;
                float right = r.Right - r.Width * 0.22f;
                float top = r.Top + r.Height * 0.28f;
                float bottom = r.Bottom - r.Height * 0.12f;
                g.DrawLine(pen, r.Left + r.Width * 0.16f, top, r.Right - r.Width * 0.16f, top);
                g.DrawLine(pen, r.Left + r.Width * 0.39f, top - r.Height * 0.1f, r.Right - r.Width * 0.39f, top - r.Height * 0.1f);
                g.DrawLine(pen, left, top + 1, left + r.Width * 0.05f, bottom - r.Height * 0.05f);
                g.DrawLine(pen, right, top + 1, right - r.Width * 0.05f, bottom - r.Height * 0.05f);
                g.DrawLine(pen, left + r.Width * 0.05f, bottom - r.Height * 0.05f, right - r.Width * 0.05f, bottom - r.Height * 0.05f);
                g.DrawLine(pen, r.Left + r.Width * 0.44f, top + r.Height * 0.08f, r.Left + r.Width * 0.44f, bottom - r.Height * 0.13f);
                g.DrawLine(pen, r.Left + r.Width * 0.56f, top + r.Height * 0.08f, r.Left + r.Width * 0.56f, bottom - r.Height * 0.13f);
            }
        }

        private void DrawInfoIcon(Graphics g, Rectangle r, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            float radius = r.Width * 0.38f;
            using (Pen pen = new Pen(color, 1.5f * _dpi))
            using (SolidBrush dot = new SolidBrush(color))
            {
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2f, radius * 2f);
                g.FillEllipse(dot, cx - 1.2f * _dpi, cy - radius * 0.52f, 2.4f * _dpi, 2.4f * _dpi);
                g.DrawLine(pen, cx, cy - radius * 0.18f, cx, cy + radius * 0.48f);
            }
        }

        private void DrawHeartIcon(Graphics g, Rectangle r, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f + 1 * _dpi;
            float s = r.Width * 0.42f;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddBezier(cx, cy + s * 0.95f,
                    cx - s * 1.35f, cy - s * 0.15f,
                    cx - s * 1.05f, cy - s * 1.25f,
                    cx - s * 0.38f, cy - s * 1.25f);
                path.AddBezier(cx - s * 0.38f, cy - s * 1.25f,
                    cx - s * 0.1f, cy - s * 1.25f,
                    cx, cy - s * 0.92f,
                    cx, cy - s * 0.72f);
                path.AddBezier(cx, cy - s * 0.72f,
                    cx, cy - s * 0.92f,
                    cx + s * 0.1f, cy - s * 1.25f,
                    cx + s * 0.38f, cy - s * 1.25f);
                path.AddBezier(cx + s * 0.38f, cy - s * 1.25f,
                    cx + s * 1.05f, cy - s * 1.25f,
                    cx + s * 1.35f, cy - s * 0.15f,
                    cx, cy + s * 0.95f);
                using (Pen pen = new Pen(color, 1.5f * _dpi))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawUpdateIcon(Graphics g, Rectangle r, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            float size = r.Width * 0.34f;
            using (Pen pen = new Pen(color, 1.5f * _dpi))
            {
                g.DrawArc(pen, cx - size, cy - size, size * 2f, size * 2f, 35f, 285f);
                g.DrawLine(pen, cx + size * 0.55f, cy - size * 0.85f, cx + size * 0.95f, cy - size * 0.55f);
                g.DrawLine(pen, cx + size * 0.55f, cy - size * 0.85f, cx + size * 0.45f, cy - size * 1.25f);
                g.DrawLine(pen, cx, cy - size * 0.55f, cx, cy + size * 0.65f);
                g.DrawLine(pen, cx - size * 0.5f, cy + size * 0.15f, cx, cy + size * 0.65f);
                g.DrawLine(pen, cx + size * 0.5f, cy + size * 0.15f, cx, cy + size * 0.65f);
            }
        }

        private void DrawToggle(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_toggleRect, 7f * _dpi))
            using (SolidBrush fill = new SolidBrush(_hoverToggle
                ? Color.FromArgb(42, 46, 56)
                : Color.FromArgb(24, 27, 33)))
            using (Pen border = new Pen(Color.FromArgb(48, 52, 62)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            float cx = _toggleRect.Left + _toggleRect.Width / 2f;
            float cy = _toggleRect.Top + _toggleRect.Height / 2f;
            float size = 3.2f * _dpi;
            using (Pen pen = new Pen(Color.FromArgb(208, 212, 220), 1.4f * _dpi))
            {
                if (_collapsed)
                {
                    g.DrawLine(pen, cx - size * 0.45f, cy - size, cx + size * 0.55f, cy);
                    g.DrawLine(pen, cx + size * 0.55f, cy, cx - size * 0.45f, cy + size);
                }
                else
                {
                    g.DrawLine(pen, cx + size * 0.45f, cy - size, cx - size * 0.55f, cy);
                    g.DrawLine(pen, cx - size * 0.55f, cy, cx + size * 0.45f, cy + size);
                }
            }
        }

        private void DrawFooter(Graphics g)
        {
            int dividerY = Height - (int)(82 * _dpi);
            using (SolidBrush line = new SolidBrush(Color.FromArgb(32, 35, 43)))
            {
                g.FillRectangle(line, (int)(16 * _dpi), dividerY, Width - (int)(32 * _dpi), 1);
            }

            Rectangle key = new Rectangle(
                (int)(16 * _dpi),
                dividerY + (int)(14 * _dpi),
                Width - (int)(32 * _dpi),
                (int)(28 * _dpi));
            using (GraphicsPath path = UiTheme.RoundedRect(key, 6f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(22, 25, 31)))
            using (Pen border = new Pen(Color.FromArgb(47, 51, 61)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(214, 209, 198)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Loc.T("截图  ", "Capture  ") + _hotkeyText, _keyFont, tb, key, sf);
            }
            using (SolidBrush muted = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("本地保存  ·  无网络", "Local  ·  Offline"), _tinyFont, muted,
                    (int)(16 * _dpi), Height - (int)(22 * _dpi));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool brand = _brandRect.Contains(e.Location);
            bool settings = _settingsRect.Contains(e.Location);
            bool recycle = _recycleRect.Contains(e.Location);
            bool reward = _rewardRect.Contains(e.Location);
            bool update = _updateRect.Contains(e.Location);
            bool about = _aboutRect.Contains(e.Location);
            bool toggle = _toggleRect.Contains(e.Location);
            if (brand != _hoverBrand || settings != _hoverSettings || recycle != _hoverRecycle ||
                reward != _hoverReward || update != _hoverUpdate || about != _hoverAbout ||
                toggle != _hoverToggle)
            {
                _hoverBrand = brand;
                _hoverSettings = settings;
                _hoverRecycle = recycle;
                _hoverReward = reward;
                _hoverUpdate = update;
                _hoverAbout = about;
                _hoverToggle = toggle;
                Cursor = brand || settings || recycle || reward || update || about || toggle
                    ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverBrand = false;
            _hoverSettings = false;
            _hoverRecycle = false;
            _hoverReward = false;
            _hoverUpdate = false;
            _hoverAbout = false;
            _hoverToggle = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            EventHandler handler = null;
            if (_toggleRect.Contains(e.Location))
            {
                handler = ToggleRequested;
            }
            else if (_brandRect.Contains(e.Location))
            {
                handler = GalleryRequested;
            }
            else if (_settingsRect.Contains(e.Location))
            {
                handler = SettingsRequested;
            }
            else if (_recycleRect.Contains(e.Location))
            {
                handler = RecycleRequested;
            }
            else if (_rewardRect.Contains(e.Location))
            {
                handler = RewardRequested;
            }
            else if (_updateRect.Contains(e.Location))
            {
                handler = UpdateRequested;
            }
            else if (_aboutRect.Contains(e.Location))
            {
                handler = AboutRequested;
            }
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_brandIcon != null)
                {
                    _brandIcon.Dispose();
                }
                _brandFont.Dispose();
                _brandSubFont.Dispose();
                _navFont.Dispose();
                _badgeFont.Dispose();
                _keyFont.Dispose();
                _tinyFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
