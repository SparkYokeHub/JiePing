using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class RewardPage : Control
    {
        private Bitmap _alipay;
        private Bitmap _wechat;
        private bool _imagesLoaded;
        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _cardTitleFont;
        private readonly Font _cardSubFont;
        private readonly Font _noticeTitleFont;
        private readonly Font _noticeBodyFont;
        private readonly Font _tinyFont;

        private float _dpi = 1f;
        private Rectangle _backRect;
        private Rectangle _alipayCard;
        private Rectangle _penguinCard;
        private Rectangle _noticeRect;
        private bool _hoverBack;

        public event EventHandler BackRequested;

        public RewardPage()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = UiTheme.Background;
            DoubleBuffered = true;
            TabStop = true;

            _titleFont = UiTheme.UiFont(21f, FontStyle.Bold);
            _subFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _cardTitleFont = UiTheme.UiFont(12.5f, FontStyle.Bold);
            _cardSubFont = UiTheme.UiFont(8.8f, FontStyle.Regular);
            _noticeTitleFont = UiTheme.UiFont(12f, FontStyle.Bold);
            _noticeBodyFont = UiTheme.UiFont(9.6f, FontStyle.Regular);
            _tinyFont = UiTheme.UiFont(8.2f, FontStyle.Regular);
        }

        private void EnsureImages()
        {
            if (_imagesLoaded)
            {
                return;
            }
            _imagesLoaded = true;
            _alipay = LoadScaledBitmap("reward_alipay.jpg", 680);
            _wechat = LoadScaledBitmap("reward_wechat.png", 680);
        }

        private static Bitmap LoadScaledBitmap(string name, int maxEdge)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Bitmap source = new Bitmap(stream))
                    {
                        float scale = Math.Min(1f, (float)maxEdge / Math.Max(source.Width, source.Height));
                        int width = Math.Max(1, (int)(source.Width * scale));
                        int height = Math.Max(1, (int)(source.Height * scale));
                        Bitmap scaled = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(scaled))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(source, new Rectangle(0, 0, width, height));
                        }
                        return scaled;
                    }
                }
            }
            catch (Exception ex)
            {
                CaptureStore.Log("reward image failed: " + name + " " + ex.Message);
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
            _backRect = new Rectangle((int)(24 * _dpi), (int)(20 * _dpi),
                (int)(92 * _dpi), (int)(30 * _dpi));

            int left = (int)(40 * _dpi);
            int gap = (int)(20 * _dpi);
            int cardWidth = Math.Max((int)(300 * _dpi), (ClientSize.Width - left * 2 - gap) / 2);
            int cardTop = (int)(136 * _dpi);
            int cardHeight = (int)(400 * _dpi);
            _alipayCard = new Rectangle(left, cardTop, cardWidth, cardHeight);
            _penguinCard = new Rectangle(left + cardWidth + gap, cardTop, cardWidth, cardHeight);

            int noticeTop = cardTop + cardHeight + (int)(20 * _dpi);
            _noticeRect = new Rectangle(left, noticeTop,
                ClientSize.Width - left * 2, (int)(210 * _dpi));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureImages();
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(UiTheme.Background))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawBackButton(g);

            using (SolidBrush title = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("赞赏", "Support"), _titleFont, title, (int)(30 * _dpi), (int)(58 * _dpi));
            }
            using (SolidBrush sub = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("如果这个工具帮到了你，可以扫码请作者喝杯咖啡。",
                        "If this tool helps you, scan a code to buy the author a coffee."), _subFont, sub,
                    (int)(33 * _dpi), (int)(105 * _dpi));
            }

            DrawRewardCard(g, _alipayCard, _alipay, Loc.T("支付宝", "Alipay"),
                Loc.T("扫码赞赏", "Scan to support"), Color.FromArgb(88, 168, 255));
            DrawRewardCard(g, _penguinCard, _wechat, Loc.T("微信", "WeChat"),
                Loc.T("微信赞赏", "WeChat support"), Color.FromArgb(90, 202, 136));

            DrawNotice(g);
        }

        private void DrawBackButton(Graphics g)
        {
            if (_hoverBack)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(_backRect, 8f * _dpi))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(28, 31, 38)))
                {
                    g.FillPath(fill, path);
                }
            }
            using (SolidBrush text = new SolidBrush(_hoverBack ? UiTheme.Text : UiTheme.Muted))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Loc.T("←  图库", "←  Library"), _subFont, text, _backRect, sf);
            }
        }

        private void DrawRewardCard(Graphics g, Rectangle card, Bitmap image, string title, string sub, Color titleColor)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(card, 14f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(44, 48, 58)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int innerPad = (int)(24 * _dpi);
            Rectangle imageArea = new Rectangle(
                card.Left + innerPad,
                card.Top + (int)(18 * _dpi),
                card.Width - innerPad * 2,
                (int)(302 * _dpi));
            DrawImageFit(g, image, imageArea, 10f * _dpi);

            using (SolidBrush tb = new SolidBrush(titleColor))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                g.DrawString(title, _cardTitleFont, tb,
                    new RectangleF(card.Left, card.Top + (int)(336 * _dpi),
                        card.Width, (int)(24 * _dpi)), sf);
            }
            using (SolidBrush sb = new SolidBrush(UiTheme.Faint))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(sub, _cardSubFont, sb,
                    new RectangleF(card.Left + (int)(16 * _dpi), card.Top + (int)(364 * _dpi),
                        card.Width - (int)(32 * _dpi), (int)(22 * _dpi)), sf);
            }
        }

        private void DrawImageFit(Graphics g, Bitmap image, Rectangle area, float radius)
        {
            if (image == null)
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(30, 33, 40)))
                using (SolidBrush text = new SolidBrush(UiTheme.Faint))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.FillRectangle(fill, area);
                    g.DrawString(Loc.T("赞赏码加载失败", "Could not load the code"), _cardSubFont, text, area, sf);
                }
                return;
            }

            float scale = Math.Min((float)area.Width / image.Width,
                (float)area.Height / image.Height);
            int width = Math.Max(1, (int)(image.Width * scale));
            int height = Math.Max(1, (int)(image.Height * scale));
            Rectangle target = new Rectangle(
                area.Left + (area.Width - width) / 2,
                area.Top + (area.Height - height) / 2,
                width,
                height);

            using (GraphicsPath clip = UiTheme.RoundedRect(
                new Rectangle(target.Left - 1, target.Top - 1, target.Width + 2, target.Height + 2),
                radius))
            {
                Region old = g.Clip.Clone();
                g.SetClip(clip);
                g.DrawImage(image, target);
                g.Clip = old;
                old.Dispose();
            }
            using (GraphicsPath border = UiTheme.RoundedRect(
                new Rectangle(target.Left - 1, target.Top - 1, target.Width + 2, target.Height + 2),
                radius))
            using (Pen pen = new Pen(Color.FromArgb(58, 62, 72)))
            {
                g.DrawPath(pen, border);
            }
        }

        private void DrawNotice(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_noticeRect, 14f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(38, 20, 21)))
            using (Pen border = new Pen(Color.FromArgb(112, 48, 43)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Rectangle bar = new Rectangle(
                _noticeRect.Left + (int)(18 * _dpi),
                _noticeRect.Top + (int)(20 * _dpi),
                (int)(3 * _dpi),
                (int)(36 * _dpi));
            using (GraphicsPath path = UiTheme.RoundedRect(bar, 1.5f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(255, 104, 90)))
            {
                g.FillPath(fill, path);
            }

            Color red = Color.FromArgb(255, 112, 101);
            Color redSoft = Color.FromArgb(255, 166, 155);
            using (SolidBrush tb = new SolidBrush(red))
            {
                g.DrawString(Loc.T("本软件完全免费使用，无任何功能上锁、无付费解锁。",
                        "This app is completely free. No features are locked and there is no paid unlock."),
                    _noticeTitleFont, tb,
                    _noticeRect.Left + (int)(34 * _dpi),
                    _noticeRect.Top + (int)(18 * _dpi));
            }
            using (SolidBrush bb = new SolidBrush(redSoft))
            {
                g.DrawString(Loc.T("赞赏属于用户自愿赠与，是对作者的鼓励，不是购买商品/服务：",
                        "Support is a voluntary gift and encouragement, not a purchase of goods or services:"),
                    _noticeBodyFont, bb,
                    _noticeRect.Left + (int)(34 * _dpi),
                    _noticeRect.Top + (int)(62 * _dpi));
                g.DrawString(Loc.T("赞赏后不承诺优先解决 BUG，全部按照顺序依次解决。",
                        "Support does not promise priority bug fixes. All issues are handled in order."),
                    _noticeBodyFont, bb,
                    _noticeRect.Left + (int)(34 * _dpi),
                    _noticeRect.Top + (int)(92 * _dpi));
            }
            using (SolidBrush tiny = new SolidBrush(Color.FromArgb(182, 122, 116)))
            {
                g.DrawString(Loc.T("赞赏完全自愿 · 感谢每一份支持", "Support is voluntary · Thank you!"),
                    _tinyFont, tiny,
                    _noticeRect.Left + (int)(34 * _dpi),
                    _noticeRect.Top + (int)(160 * _dpi));
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
            _hoverBack = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
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
                if (_alipay != null) _alipay.Dispose();
                if (_wechat != null) _wechat.Dispose();
                _titleFont.Dispose();
                _subFont.Dispose();
                _cardTitleFont.Dispose();
                _cardSubFont.Dispose();
                _noticeTitleFont.Dispose();
                _noticeBodyFont.Dispose();
                _tinyFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
