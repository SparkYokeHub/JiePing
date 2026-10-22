using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class CaptureOverlay : Form
    {
        private readonly Bitmap _screen;
        private readonly Rectangle _virtualBounds;
        private readonly Action<Bitmap, Rectangle> _callback;
        private readonly Font _badgeFont;
        private readonly Font _hintFont;
        private readonly StringFormat _ellipsis;

        private Rectangle _hoverRect;
        private string _hoverTitle = string.Empty;
        private Rectangle _dragStartHoverRect;
        private string _dragStartHoverTitle = string.Empty;
        private Point _dragStart;
        private Point _dragCurrent;
        private bool _leftDown;
        private bool _dragging;
        private bool _finished;
        private bool _callbackInvoked;
        private int _lastHoverTick;

        public CaptureOverlay(Bitmap screen, Rectangle virtualBounds, Action<Bitmap, Rectangle> callback)
        {
            _screen = screen;
            _virtualBounds = virtualBounds;
            _callback = callback;
            _badgeFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            _hintFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            _ellipsis = new StringFormat();
            _ellipsis.Trimming = StringTrimming.EllipsisCharacter;
            _ellipsis.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

            Text = Loc.T("截屏截图", "JiePing Capture");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualBounds;
            BackColor = Color.Black;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.Selectable, true);
            UpdateStyles();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public static Bitmap CaptureVirtualScreen(out Rectangle bounds)
        {
            bounds = SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = new Rectangle(0, 0, 1, 1);
            }
            Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
            bmp.SetResolution(96f, 96f);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
                Native.ForceForeground(Handle);
            }
            catch
            {
            }

            Native.POINT p;
            if (Native.GetCursorPos(out p))
            {
                Point client = new Point(p.X - _virtualBounds.Left, p.Y - _virtualBounds.Top);
                UpdateHover(client, true);
            }
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceOver;
            g.DrawImageUnscaled(_screen, 0, 0);

            using (SolidBrush veil = new SolidBrush(Color.FromArgb(118, 7, 8, 11)))
            {
                g.FillRectangle(veil, ClientRectangle);
            }

            Rectangle active = GetActiveRect();
            if (active.Width > 0 && active.Height > 0)
            {
                active.Intersect(ClientRectangle);
                if (active.Width > 0 && active.Height > 0)
                {
                    Region old = g.Clip;
                    g.SetClip(active, CombineMode.Replace);
                    g.DrawImageUnscaled(_screen, 0, 0);
                    g.Clip = old;

                    DrawSelectionFrame(g, active);
                    DrawBadge(g, active);
                }
            }

            if (!_dragging)
            {
                DrawHint(g);
            }
        }

        private Rectangle GetActiveRect()
        {
            if (_dragging)
            {
                Rectangle r = Rectangle.FromLTRB(
                    Math.Min(_dragStart.X, _dragCurrent.X),
                    Math.Min(_dragStart.Y, _dragCurrent.Y),
                    Math.Max(_dragStart.X, _dragCurrent.X),
                    Math.Max(_dragStart.Y, _dragCurrent.Y));
                if (r.Width < 2 || r.Height < 2)
                {
                    return Rectangle.Empty;
                }
                return r;
            }
            return _hoverRect;
        }

        private void DrawSelectionFrame(Graphics g, Rectangle r)
        {
            SmoothingMode oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (Pen edge = new Pen(Color.FromArgb(250, 255, 104, 72), 2f))
            {
                g.DrawRectangle(edge, r.Left + 1, r.Top + 1, Math.Max(0, r.Width - 3), Math.Max(0, r.Height - 3));
            }

            using (Pen bracket = new Pen(Color.FromArgb(255, 255, 255, 255), 2f))
            {
                int len = Math.Min(22, Math.Min(r.Width, r.Height) / 3);
                if (len >= 6)
                {
                    int l = r.Left + 3;
                    int t = r.Top + 3;
                    int rr = r.Right - 4;
                    int b = r.Bottom - 4;
                    g.DrawLine(bracket, l, t, l + len, t);
                    g.DrawLine(bracket, l, t, l, t + len);
                    g.DrawLine(bracket, rr, t, rr - len, t);
                    g.DrawLine(bracket, rr, t, rr, t + len);
                    g.DrawLine(bracket, l, b, l + len, b);
                    g.DrawLine(bracket, l, b, l, b - len);
                    g.DrawLine(bracket, rr, b, rr - len, b);
                    g.DrawLine(bracket, rr, b, rr, b - len);
                }
            }

            g.SmoothingMode = oldSmoothing;
        }

        private void DrawBadge(Graphics g, Rectangle r)
        {
            string extra = _dragging ? string.Empty : _hoverTitle;
            string size = r.Width + " × " + r.Height;
            string text = string.IsNullOrEmpty(extra) ? size : extra + "   " + size;

            float maxTextWidth = Math.Min(ClientRectangle.Width - 72, 760);
            SizeF measured = g.MeasureString(text, _badgeFont, new SizeF(maxTextWidth, 1000), _ellipsis);
            int badgeW = (int)Math.Ceiling(Math.Min(maxTextWidth, measured.Width)) + 24;
            int badgeH = 32;
            int bx = Math.Max(10, Math.Min(r.Left, ClientRectangle.Width - badgeW - 10));
            int by;
            if (r.Top >= badgeH + 18)
            {
                by = r.Top - badgeH - 9;
            }
            else
            {
                by = Math.Min(ClientRectangle.Height - badgeH - 10, r.Top + 9);
            }

            Rectangle badge = new Rectangle(bx, by, badgeW, badgeH);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(badge, 8f))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(232, 15, 17, 22)))
            using (Pen border = new Pen(Color.FromArgb(52, 255, 255, 255), 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            RectangleF textRect = new RectangleF(bx + 12, by + 6, badgeW - 24, badgeH - 10);
            using (SolidBrush white = new SolidBrush(Color.FromArgb(246, 244, 238)))
            using (SolidBrush muted = new SolidBrush(Color.FromArgb(255, 122, 96)))
            {
                if (!string.IsNullOrEmpty(extra))
                {
                    // Title in warm white, dimensions in coral at the end.  The whole
                    // string is drawn muted when it had to be trimmed.
                    g.DrawString(text, _badgeFont, white, textRect, _ellipsis);
                }
                else
                {
                    g.DrawString(text, _badgeFont, muted, textRect, _ellipsis);
                }
            }
            g.SmoothingMode = SmoothingMode.Default;
        }

        private void DrawHint(Graphics g)
        {
            string hint = Loc.T("拖动框选   ·   单击窗口截图   ·   右键 / Esc 取消",
                "Drag to select   ·   click a window   ·   right-click / Esc to cancel");
            SizeF measured = g.MeasureString(hint, _hintFont);
            int w = (int)Math.Ceiling(measured.Width) + 34;
            int h = 36;
            int x = (ClientRectangle.Width - w) / 2;
            int y = ClientRectangle.Height - h - 40;
            if (y < 12)
            {
                y = 12;
            }
            Rectangle box = new Rectangle(x, y, w, h);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(box, 17f))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(214, 12, 14, 18)))
            using (Pen border = new Pen(Color.FromArgb(38, 255, 255, 255), 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(224, 216, 210, 198)))
            using (StringFormat center = new StringFormat())
            {
                center.Alignment = StringAlignment.Center;
                center.LineAlignment = StringAlignment.Center;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.DrawString(hint, _hintFont, textBrush, box, center);
            }
            g.SmoothingMode = SmoothingMode.Default;
        }

        private static GraphicsPath RoundedRect(Rectangle r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (r.Width < d || r.Height < d)
            {
                d = Math.Min(r.Width, r.Height);
            }
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_leftDown)
            {
                _dragCurrent = ClampPoint(e.Location);
                if (!_dragging)
                {
                    int dx = Math.Abs(_dragCurrent.X - _dragStart.X);
                    int dy = Math.Abs(_dragCurrent.Y - _dragStart.Y);
                    if (dx > 5 || dy > 5)
                    {
                        _dragging = true;
                    }
                }
                if (_dragging)
                {
                    Invalidate();
                }
                return;
            }

            UpdateHover(e.Location, false);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _leftDown = true;
            _dragStart = ClampPoint(e.Location);
            _dragCurrent = _dragStart;
            _dragging = false;
            _dragStartHoverRect = _hoverRect;
            _dragStartHoverTitle = _hoverTitle;
            if (_dragStartHoverRect.IsEmpty)
            {
                UpdateHover(_dragStart, true);
                _dragStartHoverRect = _hoverRect;
                _dragStartHoverTitle = _hoverTitle;
            }
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                CancelCapture();
                return;
            }
            if (e.Button != MouseButtons.Left || !_leftDown)
            {
                return;
            }

            _leftDown = false;
            Capture = false;
            _dragCurrent = ClampPoint(e.Location);

            Rectangle target;
            if (_dragging)
            {
                target = Rectangle.FromLTRB(
                    Math.Min(_dragStart.X, _dragCurrent.X),
                    Math.Min(_dragStart.Y, _dragCurrent.Y),
                    Math.Max(_dragStart.X, _dragCurrent.X),
                    Math.Max(_dragStart.Y, _dragCurrent.Y));
                if (target.Width < 5 || target.Height < 5)
                {
                    _dragging = false;
                    Invalidate();
                    return;
                }
            }
            else
            {
                target = _dragStartHoverRect;
                if (target.IsEmpty)
                {
                    UpdateHover(_dragCurrent, true);
                    target = _hoverRect;
                }
            }

            if (!target.IsEmpty)
            {
                target.Intersect(ClientRectangle);
                CompleteCapture(target);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                CancelCapture();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                Rectangle target = _hoverRect;
                if (target.IsEmpty)
                {
                    target = ClientRectangle;
                }
                CompleteCapture(target);
                e.Handled = true;
            }
        }

        private Point ClampPoint(Point p)
        {
            int x = Math.Max(0, Math.Min(ClientRectangle.Width - 1, p.X));
            int y = Math.Max(0, Math.Min(ClientRectangle.Height - 1, p.Y));
            return new Point(x, y);
        }

        private void UpdateHover(Point clientPoint, bool force)
        {
            if (_dragging)
            {
                return;
            }
            int now = Environment.TickCount;
            if (!force && unchecked(now - _lastHoverTick) < 18)
            {
                return;
            }
            _lastHoverTick = now;

            Rectangle found;
            string title;
            if (!TryFindWindowAt(clientPoint, out found, out title))
            {
                found = ClientRectangle;
                title = Loc.T("整个屏幕", "Entire screen");
            }

            if (found != _hoverRect || !string.Equals(title, _hoverTitle, StringComparison.Ordinal))
            {
                _hoverRect = found;
                _hoverTitle = title;
                Invalidate();
            }
        }

        private bool TryFindWindowAt(Point clientPoint, out Rectangle bounds, out string title)
        {
            bounds = Rectangle.Empty;
            title = string.Empty;
            Point screenPoint = new Point(clientPoint.X + _virtualBounds.Left, clientPoint.Y + _virtualBounds.Top);

            try
            {
                System.Collections.Generic.List<IntPtr> windows = Native.GetTopLevelWindows();
                for (int i = 0; i < windows.Count; i++)
                {
                    IntPtr hwnd = windows[i];
                    if (hwnd == Handle || hwnd == IntPtr.Zero)
                    {
                        continue;
                    }
                    if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd) || Native.IsCloaked(hwnd))
                    {
                        continue;
                    }

                    string cls = Native.GetWindowClass(hwnd);
                    if (cls == "Progman" || cls == "WorkerW")
                    {
                        bounds = new Rectangle(0, 0, ClientRectangle.Width, ClientRectangle.Height);
                        title = Loc.T("桌面", "Desktop");
                        return true;
                    }

                    long exStyle = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
                    if ((exStyle & Native.WS_EX_TOOLWINDOW) != 0 &&
                        cls != "Shell_TrayWnd" && cls != "Shell_SecondaryTrayWnd")
                    {
                        continue;
                    }
                    if ((exStyle & Native.WS_EX_TRANSPARENT) != 0)
                    {
                        continue;
                    }

                    Rectangle raw = Native.GetExtendedFrameBounds(hwnd);
                    if (raw.Width < 24 || raw.Height < 24)
                    {
                        continue;
                    }
                    if (!raw.Contains(screenPoint))
                    {
                        continue;
                    }
                    raw.Intersect(_virtualBounds);
                    if (raw.Width <= 0 || raw.Height <= 0)
                    {
                        continue;
                    }

                    bounds = new Rectangle(
                        raw.Left - _virtualBounds.Left,
                        raw.Top - _virtualBounds.Top,
                        raw.Width,
                        raw.Height);

                    string windowTitle = Native.GetWindowTitle(hwnd);
                    if (string.IsNullOrEmpty(windowTitle))
                    {
                        windowTitle = cls;
                    }
                    title = windowTitle;
                    return true;
                }
            }
            catch (Exception ex)
            {
                CaptureStore.Log("window detect failed: " + ex.Message);
            }

            bounds = new Rectangle(0, 0, ClientRectangle.Width, ClientRectangle.Height);
            title = Loc.T("整个屏幕", "Entire screen");
            return true;
        }

        private void CompleteCapture(Rectangle clientRect)
        {
            if (_finished)
            {
                return;
            }

            Rectangle source = clientRect;
            source.Intersect(new Rectangle(0, 0, _screen.Width, _screen.Height));
            if (source.Width < 2 || source.Height < 2)
            {
                CancelCapture();
                return;
            }

            _finished = true;
            Bitmap result = null;
            try
            {
                result = _screen.Clone(source, PixelFormat.Format32bppPArgb);
                result.SetResolution(96f, 96f);
            }
            catch (Exception ex)
            {
                CaptureStore.Log("crop failed: " + ex.Message);
                if (result != null)
                {
                    result.Dispose();
                    result = null;
                }
            }

            Hide();
            Close();
            InvokeCallback(result, source);
        }

        private void CancelCapture()
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            Hide();
            Close();
            InvokeCallback(null, Rectangle.Empty);
        }

        private void InvokeCallback(Bitmap bitmap, Rectangle rect)
        {
            if (_callbackInvoked)
            {
                return;
            }
            _callbackInvoked = true;
            try
            {
                _callback(bitmap, rect);
            }
            catch (Exception ex)
            {
                CaptureStore.Log("capture callback failed: " + ex.Message);
                if (bitmap != null)
                {
                    bitmap.Dispose();
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (!_finished)
            {
                _finished = true;
                InvokeCallback(null, Rectangle.Empty);
            }

            _badgeFont.Dispose();
            _hintFont.Dispose();
            _ellipsis.Dispose();
            if (_screen != null)
            {
                _screen.Dispose();
            }
        }
    }
}
