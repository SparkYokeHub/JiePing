using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class GalleryPanel : Control
    {
        private sealed class GalleryItem
        {
            public ShotFile Shot;
            public Bitmap Thumb;
            public bool Loading;
            public bool Failed;
            public int Width;
            public int Height;

            public GalleryItem(ShotFile shot)
            {
                Shot = shot;
            }
        }

        public event Action<ShotFile> ShotActivated;
        public event EventHandler ItemsChanged;
        public event Action<string, bool> StatusMessage;

        private readonly GalleryMode _mode;
        private readonly List<GalleryItem> _items = new List<GalleryItem>();
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Semaphore _thumbGate = new Semaphore(3, 3);
        private readonly Font _timeFont;
        private readonly Font _emptyTitleFont;
        private readonly Font _emptySubFont;
        private readonly Font _tinyFont;
        private readonly Font _toolbarTitleFont;
        private readonly Font _toolbarFont;

        private float _dpi = 1f;
        private int _columns = 3;
        private int _cardWidth = 340;
        private int _cardHeight = 218;
        private int _gap = 18;
        private int _padX = 24;
        private int _padTop = 22;
        private int _padBottom = 30;
        private int _scroll;
        private int _hoverIndex = -1;
        private bool _scrollbarHover;
        private bool _deleteHover;
        private bool _deletePressed;
        private bool _dirty = true;
        private bool _menuOpen;
        private bool _dragging;
        private bool _suppressClick;
        private bool _selectionMode;
        private string _hotkeyText = "Ctrl + Alt";
        private int _mouseDownIndex = -1;
        private Point _mouseDownPoint;
        private int _toolbarHeight = 64;
        private int _toolbarHover = -1;
        private Rectangle _selectButtonRect;
        private Rectangle _selectAllRect;
        private Rectangle _batchCancelRect;
        private Rectangle _batchDeleteRect;

        public GalleryPanel(GalleryMode mode)
        {
            _mode = mode;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = UiTheme.Background;
            TabStop = true;
            DoubleBuffered = true;

            _timeFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _emptyTitleFont = UiTheme.UiFont(16.5f, FontStyle.Bold);
            _emptySubFont = UiTheme.UiFont(9.5f, FontStyle.Regular);
            _tinyFont = UiTheme.UiFont(8.5f, FontStyle.Regular);
            _toolbarTitleFont = UiTheme.UiFont(10.5f, FontStyle.Bold);
            _toolbarFont = UiTheme.UiFont(9f, FontStyle.Bold);
        }

        public int Count
        {
            get { return _items.Count; }
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

        public GalleryMode Mode
        {
            get { return _mode; }
        }

        public bool IsSelecting
        {
            get { return _selectionMode; }
        }

        public int SelectionCount
        {
            get { return _selectedPaths.Count; }
        }

        public bool HandleEscape()
        {
            if (!_selectionMode)
            {
                return false;
            }
            ExitSelection();
            return true;
        }

        public bool HandleDeleteSelection()
        {
            if (!_selectionMode || _selectedPaths.Count == 0)
            {
                return false;
            }
            DeleteSelected();
            return true;
        }

        public bool HandleSelectAll()
        {
            if (_items.Count == 0)
            {
                return false;
            }
            if (!_selectionMode)
            {
                EnterSelection();
            }
            ToggleSelectAll();
            return true;
        }

        public void CancelSelection()
        {
            if (_selectionMode || _selectedPaths.Count > 0)
            {
                ExitSelection();
            }
        }

        private void ToggleSelected(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }
            string path = _items[index].Shot.Path;
            if (_selectedPaths.Contains(path))
            {
                _selectedPaths.Remove(path);
            }
            else
            {
                _selectedPaths.Add(path);
            }
            Invalidate();
        }

        private void ToggleSelectAll()
        {
            if (_selectedPaths.Count == _items.Count && _items.Count > 0)
            {
                _selectedPaths.Clear();
            }
            else
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    _selectedPaths.Add(_items[i].Shot.Path);
                }
            }
            Invalidate();
        }

        private void DeleteSelected()
        {
            if (_selectedPaths.Count == 0)
            {
                return;
            }
            List<ShotFile> selected = new List<ShotFile>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_selectedPaths.Contains(_items[i].Shot.Path))
                {
                    selected.Add(_items[i].Shot);
                }
            }
            if (selected.Count == 0)
            {
                return;
            }

            if (_mode == GalleryMode.Recycle)
            {
                using (ConfirmDialog dialog = new ConfirmDialog(
                    Loc.Format("彻底删除 {0} 张截图？", "Delete {0} screenshots forever?", selected.Count),
                    Loc.T("此操作会从计算机永久删除这些图片，无法恢复。",
                        "This permanently removes these images from your computer."),
                    Loc.T("彻底删除", "Delete Forever")))
                {
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK || !dialog.Confirmed)
                    {
                        return;
                    }
                }
            }

            int ok = 0;
            int failed = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                if (_mode == GalleryMode.Recycle)
                {
                    if (CaptureStore.DeletePermanently(selected[i].Path))
                    {
                        ok++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(CaptureStore.MoveToRecycle(selected[i].Path)))
                    {
                        ok++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            Reload(false);
            NotifyChanged();
            if (failed == 0)
            {
                NotifyStatus(_mode == GalleryMode.Recycle
                    ? Loc.Format("已彻底删除 {0} 张截图", "Deleted {0} screenshots", ok)
                    : Loc.Format("已将 {0} 张截图移入回收站", "Moved {0} screenshots to the Recycle Bin", ok),
                    false);
            }
            else
            {
                NotifyStatus(Loc.Format("完成 {0} 张，失败 {1} 张", "Done: {0}, failed: {1}", ok, failed), true);
            }
        }

        private void EnterSelection()
        {
            _selectionMode = true;
            _hoverIndex = -1;
            _deleteHover = false;
            _toolbarHover = -1;
            Invalidate();
        }

        private void ExitSelection()
        {
            _selectionMode = false;
            _selectedPaths.Clear();
            _hoverIndex = -1;
            _deleteHover = false;
            _toolbarHover = -1;
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
            _dirty = true;
            if (ReallyVisible())
            {
                Reload(true);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (ReallyVisible() && _dirty && IsHandleCreated)
            {
                RefreshIfNeeded();
            }
        }

        private bool ReallyVisible()
        {
            Form form = FindForm();
            return Visible && form != null && form.Visible;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void RefreshIfNeeded()
        {
            if (_dirty)
            {
                Reload(true);
            }
            else
            {
                QueueVisibleThumbs();
                Invalidate();
            }
        }

        public void Reload(bool scrollTop)
        {
            int oldScroll = _scroll;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Thumb != null)
                {
                    _items[i].Thumb.Dispose();
                    _items[i].Thumb = null;
                }
            }
            _items.Clear();
            _hoverIndex = -1;
            _deleteHover = false;
            _selectionMode = false;
            _selectedPaths.Clear();
            _toolbarHover = -1;

            List<ShotFile> files = _mode == GalleryMode.Recycle
                ? CaptureStore.LoadRecycled()
                : CaptureStore.LoadAll();
            for (int i = 0; i < files.Count; i++)
            {
                _items.Add(new GalleryItem(files[i]));
            }
            _dirty = false;

            RecalcLayout();
            _scroll = scrollTop ? 0 : ClampScroll(oldScroll);
            QueueVisibleThumbs();
            Invalidate();
        }

        private void RecalcLayout()
        {
            int width = Math.Max(1, ClientSize.Width);
            int pad = (int)Math.Round(24 * _dpi);
            int gap = (int)Math.Round(18 * _dpi);
            int minCard = (int)Math.Round(300 * _dpi);
            int usable = Math.Max(120, width - pad * 2);

            int cols = Math.Max(1, (usable + gap) / (minCard + gap));
            if (cols > 6)
            {
                cols = 6;
            }
            _columns = cols;
            _gap = gap;
            _padX = pad;
            _toolbarHeight = (int)Math.Round(64 * _dpi);
            _padTop = _toolbarHeight + (int)Math.Round(8 * _dpi);
            _padBottom = (int)Math.Round(30 * _dpi);
            _cardWidth = Math.Max(80, (usable - gap * (cols - 1)) / cols);
            _cardHeight = Math.Max(64, (int)Math.Round(_cardWidth * 0.635));

            int right = Math.Max(pad, width - pad);
            int top = (int)Math.Round(17 * _dpi);
            int height = (int)Math.Round(30 * _dpi);
            _selectButtonRect = new Rectangle(right - (int)(100 * _dpi), top, (int)(100 * _dpi), height);

            int deleteWidth = (int)((_mode == GalleryMode.Recycle ? 106 : 84) * _dpi);
            _batchDeleteRect = new Rectangle(right - deleteWidth, top, deleteWidth, height);
            int cancelWidth = (int)(62 * _dpi);
            _batchCancelRect = new Rectangle(_batchDeleteRect.Left - (int)(8 * _dpi) - cancelWidth,
                top, cancelWidth, height);
            int allWidth = (int)(68 * _dpi);
            _selectAllRect = new Rectangle(_batchCancelRect.Left - (int)(8 * _dpi) - allWidth,
                top, allWidth, height);
        }

        private int ContentHeight
        {
            get
            {
                if (_items.Count == 0)
                {
                    return ClientSize.Height;
                }
                int rows = (int)Math.Ceiling(_items.Count / (double)_columns);
                return _padTop + rows * (_cardHeight + _gap) - _gap + _padBottom;
            }
        }

        private int MaxScroll
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }

        private int ClampScroll(int value)
        {
            return Math.Max(0, Math.Min(MaxScroll, value));
        }

        private Rectangle RectForIndex(int index, bool includeScroll)
        {
            if (index < 0 || index >= _items.Count)
            {
                return Rectangle.Empty;
            }
            int col = index % _columns;
            int row = index / _columns;
            int x = _padX + col * (_cardWidth + _gap);
            int y = _padTop + row * (_cardHeight + _gap) - (includeScroll ? _scroll : 0);
            return new Rectangle(x, y, _cardWidth, _cardHeight);
        }

        private Rectangle DeleteButtonRect(Rectangle card)
        {
            int width = (int)((_mode == GalleryMode.Recycle ? 88 : 66) * _dpi);
            int height = (int)(30 * _dpi);
            width = Math.Min(width, Math.Max(40, card.Width - (int)(20 * _dpi)));
            int margin = (int)(10 * _dpi);
            return new Rectangle(card.Right - width - margin, card.Bottom - height - margin, width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(UiTheme.Background))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            if (_items.Count == 0)
            {
                DrawEmptyState(g);
                return;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int firstRow = Math.Max(0, _scroll / Math.Max(1, _cardHeight + _gap));
            int lastRow = Math.Min(
                (int)Math.Ceiling(_items.Count / (double)_columns) - 1,
                firstRow + ClientSize.Height / Math.Max(1, _cardHeight + _gap) + 1);
            int start = firstRow * _columns;
            int end = Math.Min(_items.Count, (lastRow + 1) * _columns);

            for (int i = start; i < end; i++)
            {
                Rectangle rect = RectForIndex(i, true);
                if (rect.Bottom < 0 || rect.Top > ClientSize.Height)
                {
                    continue;
                }
                DrawCard(g, i, rect);
            }

            DrawScrollbar(g);
            DrawToolbar(g);
        }

        private void DrawCard(Graphics g, int index, Rectangle rect)
        {
            GalleryItem item = _items[index];
            bool hover = index == _hoverIndex;
            bool selected = _selectionMode && _selectedPaths.Contains(item.Shot.Path);
            float radius = 11f * _dpi;

            if (item.Thumb == null && !item.Loading && !item.Failed)
            {
                QueueThumb(item);
            }

            using (GraphicsPath path = UiTheme.RoundedRect(rect, radius))
            {
                using (SolidBrush cardBg = new SolidBrush(UiTheme.Card))
                {
                    g.FillPath(cardBg, path);
                }

                Region oldClip = g.Clip;
                g.SetClip(path, CombineMode.Intersect);

                if (item.Thumb != null)
                {
                    Rectangle imageRect = FitRect(item.Thumb.Width, item.Thumb.Height, rect);
                    g.DrawImage(item.Thumb, imageRect);
                    DrawBottomShade(g, rect);
                    DrawTime(g, rect, item.Shot.Time);
                }
                else
                {
                    using (SolidBrush imageBg = new SolidBrush(UiTheme.CardImage))
                    {
                        g.FillRectangle(imageBg, rect);
                    }
                    DrawThumbPlaceholder(g, rect, item);
                }

                g.Clip = oldClip;
            }

            Color borderColor = selected || hover ? UiTheme.Accent : UiTheme.Border;
            float borderWidth = selected ? 2.2f : (hover ? 1.6f : 1f);
            using (Pen border = new Pen(borderColor, borderWidth))
            {
                using (GraphicsPath path = UiTheme.RoundedRect(rect, radius))
                {
                    g.DrawPath(border, path);
                }
            }

            if (hover)
            {
                Rectangle shine = new Rectangle(rect.Left + 1, rect.Top + 1, Math.Max(0, rect.Width - 2), 1);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                {
                    g.FillRectangle(b, shine);
                }
            }

            if (_selectionMode)
            {
                DrawSelectionMark(g, rect, selected);
            }
            else if (hover)
            {
                DrawDeleteButton(g, rect, index);
            }
        }

        private void DrawSelectionMark(Graphics g, Rectangle card, bool selected)
        {
            int size = (int)(22 * _dpi);
            Rectangle mark = new Rectangle(card.Right - size - (int)(10 * _dpi),
                card.Top + (int)(10 * _dpi), size, size);
            using (SolidBrush fill = new SolidBrush(selected
                ? UiTheme.Accent
                : Color.FromArgb(205, 12, 14, 19)))
            using (Pen border = new Pen(selected
                ? Color.FromArgb(230, 255, 255, 255)
                : Color.FromArgb(120, 255, 255, 255), 1.2f))
            {
                g.FillEllipse(fill, mark);
                g.DrawEllipse(border, mark);
            }
            if (selected)
            {
                using (Pen check = new Pen(Color.FromArgb(40, 20, 15), 2f))
                {
                    g.DrawLines(check, new PointF[]
                    {
                        new PointF(mark.Left + mark.Width * 0.28f, mark.Top + mark.Height * 0.52f),
                        new PointF(mark.Left + mark.Width * 0.44f, mark.Top + mark.Height * 0.68f),
                        new PointF(mark.Left + mark.Width * 0.73f, mark.Top + mark.Height * 0.33f)
                    });
                }
            }
        }

        private void DrawToolbar(Graphics g)
        {
            int h = _toolbarHeight;
            using (SolidBrush bg = new SolidBrush(UiTheme.Background))
            {
                g.FillRectangle(bg, 0, 0, Width, h);
            }
            using (SolidBrush line = new SolidBrush(Color.FromArgb(34, 37, 45)))
            {
                g.FillRectangle(line, 0, h - 1, Width, 1);
            }

            string title = _selectionMode
                ? Loc.Format("已选 {0} 张", "{0} selected", _selectedPaths.Count)
                : (_mode == GalleryMode.Recycle
                    ? Loc.T("回收站图片", "Recycle Bin")
                    : Loc.T("全部截图", "All Screenshots"));
            float titleY = (h - _toolbarTitleFont.GetHeight(g)) / 2f - 1;
            using (SolidBrush tb = new SolidBrush(_selectionMode ? UiTheme.Accent : UiTheme.Text))
            {
                g.DrawString(title, _toolbarTitleFont, tb, _padX, titleY);
            }
            if (!_selectionMode)
            {
                string count = Loc.Items(_items.Count);
                SizeF titleSize = g.MeasureString(title, _toolbarTitleFont);
                using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
                {
                    g.DrawString(count, _tinyFont, muted,
                        _padX + titleSize.Width + 10 * _dpi,
                        h / 2f - _tinyFont.GetHeight(g) / 2f);
                }
                DrawSelectButton(g);
            }
            else
            {
                DrawToolbarButton(g, _selectAllRect,
                    _selectedPaths.Count == _items.Count && _items.Count > 0
                        ? Loc.T("取消全选", "Deselect All") : Loc.T("全选", "Select All"),
                    false, false, _items.Count > 0, _toolbarHover == 1);
                DrawToolbarButton(g, _batchCancelRect, Loc.T("取消", "Cancel"),
                    false, false, true, _toolbarHover == 2);
                DrawToolbarButton(g, _batchDeleteRect,
                    _mode == GalleryMode.Recycle
                        ? Loc.T("彻底删除", "Delete Forever")
                        : Loc.T("移入回收站", "Move to Recycle Bin"),
                    _mode == GalleryMode.Recycle, true,
                    _selectedPaths.Count > 0, _toolbarHover == 3);
            }
        }

        private void DrawSelectButton(Graphics g)
        {
            Rectangle r = _selectButtonRect;
            bool hover = _toolbarHover == 0;
            using (GraphicsPath path = UiTheme.RoundedRect(r, r.Height / 2f))
            using (SolidBrush fill = new SolidBrush(hover
                ? Color.FromArgb(43, 47, 57)
                : Color.FromArgb(27, 30, 37)))
            using (Pen border = new Pen(Color.FromArgb(hover ? 95 : 55, 255, 255, 255)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Rectangle icon = new Rectangle(r.Left + (int)(12 * _dpi),
                r.Top + (r.Height - (int)(13 * _dpi)) / 2, (int)(13 * _dpi), (int)(13 * _dpi));
            using (Pen pen = new Pen(UiTheme.Text, 1.3f))
            {
                using (GraphicsPath path = UiTheme.RoundedRect(icon, 2.5f * _dpi))
                {
                    g.DrawPath(pen, path);
                }
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(icon.Left + icon.Width * 0.24f, icon.Top + icon.Height * 0.53f),
                    new PointF(icon.Left + icon.Width * 0.44f, icon.Top + icon.Height * 0.73f),
                    new PointF(icon.Left + icon.Width * 0.78f, icon.Top + icon.Height * 0.29f)
                });
            }
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Loc.T("批量选择", "Select"), _toolbarFont, text,
                    new RectangleF(icon.Right + 7 * _dpi, r.Top, r.Right - icon.Right - 10 * _dpi, r.Height), sf);
            }
        }

        private void DrawToolbarButton(Graphics g, Rectangle r, string label, bool danger, bool primary, bool enabled, bool hover)
        {
            Color fill;
            Color border;
            Color text;
            if (!enabled)
            {
                fill = Color.FromArgb(25, 28, 34);
                border = Color.FromArgb(40, 44, 53);
                text = UiTheme.Faint;
            }
            else if (danger)
            {
                fill = hover ? Color.FromArgb(255, 117, 109) : Color.FromArgb(246, 92, 84);
                border = Color.FromArgb(255, 137, 127);
                text = Color.FromArgb(40, 18, 15);
            }
            else if (primary)
            {
                fill = hover ? Color.FromArgb(255, 133, 105) : UiTheme.Accent;
                border = Color.FromArgb(255, 151, 126);
                text = Color.FromArgb(38, 20, 15);
            }
            else
            {
                fill = hover ? Color.FromArgb(44, 48, 58) : Color.FromArgb(28, 31, 38);
                border = Color.FromArgb(hover ? 90 : 50, 255, 255, 255);
                text = UiTheme.Text;
            }

            using (GraphicsPath path = UiTheme.RoundedRect(r, r.Height / 2f))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (SolidBrush tb = new SolidBrush(text))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(label, _toolbarFont, tb, r, sf);
            }
        }

        private void DrawDeleteButton(Graphics g, Rectangle card, int index)
        {
            Rectangle button = DeleteButtonRect(card);
            bool active = _deleteHover && index == _hoverIndex;
            Color accent = _mode == GalleryMode.Recycle
                ? Color.FromArgb(255, 95, 95)
                : UiTheme.Accent;
            float radius = button.Height / 2f;

            for (int i = 3; i >= 1; i--)
            {
                Rectangle shadow = button;
                shadow.Offset(0, i);
                using (GraphicsPath path = UiTheme.RoundedRect(shadow, radius))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(14 + i * 5, 0, 0, 0)))
                {
                    g.FillPath(b, path);
                }
            }
            if (active)
            {
                using (GraphicsPath path = UiTheme.RoundedRect(Rectangle.Inflate(button, 4, 4), radius + 4))
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(38, accent)))
                {
                    g.FillPath(glow, path);
                }
            }

            using (GraphicsPath path = UiTheme.RoundedRect(button, radius))
            {
                if (active)
                {
                    Color top = _mode == GalleryMode.Recycle
                        ? Color.FromArgb(255, 117, 117)
                        : Color.FromArgb(255, 134, 104);
                    using (LinearGradientBrush fill = new LinearGradientBrush(
                        button, top, accent, LinearGradientMode.Vertical))
                    {
                        g.FillPath(fill, path);
                    }
                }
                else
                {
                    using (LinearGradientBrush fill = new LinearGradientBrush(
                        button, Color.FromArgb(245, 38, 42, 52), Color.FromArgb(245, 19, 21, 27), LinearGradientMode.Vertical))
                    {
                        g.FillPath(fill, path);
                    }
                }
                using (Pen border = new Pen(active
                    ? Color.FromArgb(180, 255, 255, 255)
                    : Color.FromArgb(70, 255, 255, 255), 1f))
                {
                    g.DrawPath(border, path);
                }
            }

            Rectangle iconRect = new Rectangle(
                button.Left + (int)(9 * _dpi),
                button.Top + (button.Height - (int)(16 * _dpi)) / 2,
                (int)(16 * _dpi),
                (int)(16 * _dpi));
            Color iconColor = active ? Color.FromArgb(40, 20, 15) : Color.FromArgb(240, 237, 230);
            DrawTrashGlyph(g, iconRect, iconColor);

            string label = _mode == GalleryMode.Recycle
                ? Loc.T("彻底删除", "Delete")
                : Loc.T("删除", "Delete");
            using (SolidBrush text = new SolidBrush(active
                ? Color.FromArgb(38, 18, 14)
                : Color.FromArgb(238, 234, 226)))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(label, _tinyFont, text,
                    new RectangleF(iconRect.Right + (int)(5 * _dpi), button.Top,
                        button.Right - iconRect.Right - (int)(8 * _dpi), button.Height), sf);
            }

            if (active)
            {
                string tip = _mode == GalleryMode.Recycle
                    ? Loc.T("彻底删除并永久移除", "Delete permanently")
                    : Loc.T("移入回收站", "Move to Recycle Bin");
                SizeF measured = g.MeasureString(tip, _tinyFont);
                int tipW = (int)Math.Ceiling(measured.Width) + 18;
                int tipH = (int)(25 * _dpi);
                int tipX = Math.Max(card.Left + 8, button.Right - tipW);
                int tipY = button.Top - tipH - (int)(8 * _dpi);
                if (tipY < card.Top + 8)
                {
                    tipY = button.Bottom + (int)(8 * _dpi);
                }
                Rectangle tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                using (GraphicsPath path = UiTheme.RoundedRect(tipRect, 6f * _dpi))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(242, 18, 20, 26)))
                using (Pen border = new Pen(Color.FromArgb(58, 255, 255, 255)))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }
                using (SolidBrush text = new SolidBrush(Color.FromArgb(244, 240, 233)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(tip, _tinyFont, text, tipRect, sf);
                }
            }
        }

        private void DrawTrashGlyph(Graphics g, Rectangle icon, Color color)
        {
            float left = icon.Left + icon.Width * 0.22f;
            float right = icon.Right - icon.Width * 0.22f;
            float top = icon.Top + icon.Height * 0.34f;
            float bottom = icon.Bottom - icon.Height * 0.16f;
            using (Pen pen = new Pen(color, 1.4f * _dpi))
            {
                g.DrawLine(pen, left - 1, top, right + 1, top);
                g.DrawLine(pen, left + (right - left) * 0.28f, top - icon.Height * 0.11f,
                    right - (right - left) * 0.28f, top - icon.Height * 0.11f);
                g.DrawLine(pen, left, top, left + (right - left) * 0.1f, bottom);
                g.DrawLine(pen, right, top, right - (right - left) * 0.1f, bottom);
                g.DrawLine(pen, left + (right - left) * 0.1f, bottom,
                    right - (right - left) * 0.1f, bottom);
                g.DrawLine(pen, icon.Left + icon.Width * 0.5f, top + icon.Height * 0.13f,
                    icon.Left + icon.Width * 0.5f, bottom - icon.Height * 0.12f);
            }
        }

        private static Rectangle FitRect(int imageWidth, int imageHeight, Rectangle bounds)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return bounds;
            }
            double scale = Math.Min((double)bounds.Width / imageWidth, (double)bounds.Height / imageHeight);
            int w = Math.Max(1, (int)Math.Round(imageWidth * scale));
            int h = Math.Max(1, (int)Math.Round(imageHeight * scale));
            return new Rectangle(
                bounds.Left + (bounds.Width - w) / 2,
                bounds.Top + (bounds.Height - h) / 2,
                w,
                h);
        }

        private void DrawBottomShade(Graphics g, Rectangle rect)
        {
            int shadeHeight = Math.Min(rect.Height, Math.Max(56, (int)(rect.Height * 0.34)));
            Rectangle shade = new Rectangle(rect.Left, rect.Bottom - shadeHeight, rect.Width, shadeHeight);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                shade, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(178, 4, 5, 7), LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, shade);
            }
        }

        private void DrawTime(Graphics g, Rectangle rect, DateTime time)
        {
            string label = FormatTime(time);
            float x = rect.Left + 15 * _dpi;
            float y = rect.Bottom - 26 * _dpi;
            using (SolidBrush dot = new SolidBrush(UiTheme.Accent))
            {
                float d = 4.2f * _dpi;
                g.FillEllipse(dot, x, y + 6 * _dpi, d, d);
            }
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.DrawString(label, _timeFont, shadow, x + 12 * _dpi, y + 1);
            }
            using (SolidBrush text = new SolidBrush(Color.FromArgb(242, 240, 234)))
            {
                g.DrawString(label, _timeFont, text, x + 11 * _dpi, y);
            }
        }

        private string FormatTime(DateTime time)
        {
            DateTime today = DateTime.Today;
            if (time.Date == today || time.Date == today.AddDays(-1))
            {
                return Loc.TimeLabel(time);
            }
            if (time.Year == today.Year)
            {
                return Loc.IsEnglish
                    ? time.ToString("MMM d  HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                    : time.ToString("M月d日  HH:mm");
            }
            return Loc.LongDateTime(time);
        }

        private void DrawThumbPlaceholder(Graphics g, Rectangle rect, GalleryItem item)
        {
            string text = item.Failed
                ? Loc.T("无法读取", "Unreadable")
                : Loc.T("载入中", "Loading");
            using (Font f = UiTheme.UiFont(9f, FontStyle.Regular))
            using (SolidBrush b = new SolidBrush(UiTheme.Faint))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, f, b, rect, sf);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int cx = ClientSize.Width / 2;
            int cy = Math.Max(180, (int)(ClientSize.Height * 0.42));

            if (_mode == GalleryMode.Recycle)
            {
                DrawEmptyTrashIcon(g, cx, cy - (int)(6 * _dpi));
            }
            else
            {
                int box = (int)(76 * _dpi);
                int left = cx - box / 2;
                int top = cy - box / 2;
                using (Pen frame = new Pen(Color.FromArgb(88, 96, 108), 2.4f * _dpi))
                {
                    int arm = (int)(box * 0.29);
                    int r = left + box;
                    int b = top + box;
                    g.DrawLine(frame, left, top, left + arm, top);
                    g.DrawLine(frame, left, top, left, top + arm);
                    g.DrawLine(frame, r, top, r - arm, top);
                    g.DrawLine(frame, r, top, r, top + arm);
                    g.DrawLine(frame, left, b, left + arm, b);
                    g.DrawLine(frame, left, b, left, b - arm);
                    g.DrawLine(frame, r, b, r - arm, b);
                    g.DrawLine(frame, r, b, r, b - arm);
                }
                using (SolidBrush dot = new SolidBrush(UiTheme.Accent))
                {
                    float d = 9 * _dpi;
                    g.FillEllipse(dot, cx - d / 2, cy - d / 2, d, d);
                }
            }

            string title = _mode == GalleryMode.Recycle
                ? Loc.T("回收站是空的", "The Recycle Bin is empty")
                : Loc.T("按 ", "Press ") + _hotkeyText + Loc.T(" 截取第一张", " to capture your first screenshot");
            string subtitle = _mode == GalleryMode.Recycle
                ? Loc.T("删除的截图会先放在这里，再点删除才会永久移除。",
                    "Deleted screenshots appear here first. Delete again to remove them permanently.")
                : Loc.T("截图会自动按时间顺序出现在这里，图片可以直接拖拽发送。",
                    "Screenshots appear here in time order. Drag them out to send.");

            Rectangle titleRect = new Rectangle(20, cy + (int)(52 * _dpi), ClientSize.Width - 40, 38);
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                g.DrawString(title, _emptyTitleFont, text, titleRect, sf);
            }

            Rectangle subRect = new Rectangle(20, titleRect.Bottom + 4, ClientSize.Width - 40, 28);
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                g.DrawString(subtitle, _emptySubFont, muted, subRect, sf);
            }

            string hint = _mode == GalleryMode.Recycle
                ? Loc.T("悬停图片可彻底删除   ·   也可以拖拽发送",
                    "Hover a screenshot to delete it   ·   drag to send")
                : Loc.T("拖动框选内容   ·   悬停单击窗口   ·   图片可拖拽发送",
                    "Drag to select   ·   click a window   ·   drag images to send");
            SizeF size = g.MeasureString(hint, _tinyFont);
            Rectangle hintRect = new Rectangle(
                (ClientSize.Width - (int)size.Width) / 2,
                ClientSize.Height - (int)(54 * _dpi),
                (int)size.Width + 2,
                (int)size.Height + 2);
            using (SolidBrush faint = new SolidBrush(Color.FromArgb(112, 118, 130)))
            {
                g.DrawString(hint, _tinyFont, faint, hintRect);
            }
        }

        private void DrawEmptyTrashIcon(Graphics g, int cx, int cy)
        {
            int w = (int)(58 * _dpi);
            int h = (int)(68 * _dpi);
            int left = cx - w / 2;
            int top = cy - h / 2;
            using (Pen pen = new Pen(Color.FromArgb(88, 96, 108), 2.4f * _dpi))
            {
                g.DrawLine(pen, left + w * 0.14f, top + h * 0.24f, left + w * 0.86f, top + h * 0.24f);
                g.DrawLine(pen, left + w * 0.38f, top + h * 0.16f, left + w * 0.62f, top + h * 0.16f);
                g.DrawLine(pen, left + w * 0.22f, top + h * 0.24f, left + w * 0.3f, top + h * 0.9f);
                g.DrawLine(pen, left + w * 0.78f, top + h * 0.24f, left + w * 0.7f, top + h * 0.9f);
                g.DrawLine(pen, left + w * 0.3f, top + h * 0.9f, left + w * 0.7f, top + h * 0.9f);
                g.DrawLine(pen, left + w * 0.42f, top + h * 0.36f, left + w * 0.42f, top + h * 0.76f);
                g.DrawLine(pen, left + w * 0.58f, top + h * 0.36f, left + w * 0.58f, top + h * 0.76f);
            }
        }

        private void DrawScrollbar(Graphics g)
        {
            if (MaxScroll <= 0)
            {
                return;
            }
            int trackTop = _toolbarHeight + 8;
            int trackHeight = Math.Max(1, ClientSize.Height - trackTop - 10);
            int thumbHeight = Math.Max((int)(38 * _dpi), (int)(trackHeight * (double)ClientSize.Height / ContentHeight));
            thumbHeight = Math.Min(trackHeight, thumbHeight);
            int thumbTop = trackTop + (int)((trackHeight - thumbHeight) * (_scroll / (double)MaxScroll));
            int width = (int)Math.Round(4 * _dpi);
            Rectangle thumb = new Rectangle(ClientSize.Width - width - (int)(5 * _dpi), thumbTop, width, thumbHeight);
            using (GraphicsPath path = UiTheme.RoundedRect(thumb, width / 2f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(_scrollbarHover ? 110 : 62, 198, 202, 212)))
            {
                g.FillPath(b, path);
            }
        }

        private int HitTest(Point p)
        {
            if (_items.Count == 0 || p.Y < _toolbarHeight)
            {
                return -1;
            }
            int relX = p.X - _padX;
            int relY = p.Y + _scroll - _padTop;
            if (relX < 0 || relY < 0)
            {
                return -1;
            }
            int col = relX / Math.Max(1, _cardWidth + _gap);
            int row = relY / Math.Max(1, _cardHeight + _gap);
            int index = row * _columns + col;
            if (index < 0 || index >= _items.Count)
            {
                return -1;
            }
            Rectangle rect = RectForIndex(index, true);
            return rect.Contains(p) ? index : -1;
        }

        private bool IsDeleteButtonHit(int index, Point p)
        {
            return index >= 0 && DeleteButtonRect(RectForIndex(index, true)).Contains(p);
        }

        private int ToolbarHit(Point p)
        {
            if (_items.Count == 0 || p.Y >= _toolbarHeight)
            {
                return -1;
            }
            if (!_selectionMode)
            {
                return _selectButtonRect.Contains(p) ? 0 : -1;
            }
            if (_batchDeleteRect.Contains(p) && _selectedPaths.Count > 0)
            {
                return 3;
            }
            if (_batchCancelRect.Contains(p))
            {
                return 2;
            }
            if (_selectAllRect.Contains(p))
            {
                return 1;
            }
            return -1;
        }

        private bool HandleToolbarClick(Point p)
        {
            if (_items.Count == 0 || p.Y >= _toolbarHeight)
            {
                return false;
            }
            if (!_selectionMode)
            {
                if (_selectButtonRect.Contains(p))
                {
                    EnterSelection();
                }
                return true;
            }
            if (_batchDeleteRect.Contains(p) && _selectedPaths.Count > 0)
            {
                DeleteSelected();
                return true;
            }
            if (_batchCancelRect.Contains(p))
            {
                ExitSelection();
                return true;
            }
            if (_selectAllRect.Contains(p))
            {
                ToggleSelectAll();
                return true;
            }
            return true;
        }

        private void InvalidateCard(int index)
        {
            Rectangle rect = RectForIndex(index, true);
            if (!rect.IsEmpty)
            {
                Invalidate(Rectangle.Inflate(rect, 8, 8));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int toolbar = ToolbarHit(e.Location);
            if (e.Y < _toolbarHeight)
            {
                if (toolbar != _toolbarHover)
                {
                    _toolbarHover = toolbar;
                    Invalidate(new Rectangle(0, 0, Width, _toolbarHeight));
                }
                if (_hoverIndex != -1)
                {
                    InvalidateCard(_hoverIndex);
                    _hoverIndex = -1;
                    _deleteHover = false;
                }
                Cursor = toolbar >= 0 ? Cursors.Hand : Cursors.Default;
                return;
            }
            if (_toolbarHover != -1)
            {
                _toolbarHover = -1;
                Invalidate(new Rectangle(0, 0, Width, _toolbarHeight));
            }

            if (!_selectionMode && _mouseDownIndex >= 0 && !_deletePressed && !_dragging &&
                (Control.MouseButtons & MouseButtons.Left) != 0)
            {
                int dx = Math.Abs(e.X - _mouseDownPoint.X);
                int dy = Math.Abs(e.Y - _mouseDownPoint.Y);
                if (dx > 6 || dy > 6)
                {
                    BeginDrag(_mouseDownIndex);
                    return;
                }
            }

            int hit = HitTest(e.Location);
            bool scrollbar = MaxScroll > 0 && e.X >= ClientSize.Width - (int)(14 * _dpi);
            if (hit != _hoverIndex)
            {
                InvalidateCard(_hoverIndex);
                _hoverIndex = hit;
                InvalidateCard(hit);
                _deleteHover = false;
            }

            bool deleteHover = !_selectionMode && IsDeleteButtonHit(hit, e.Location);
            if (deleteHover != _deleteHover)
            {
                _deleteHover = deleteHover;
                InvalidateCard(hit);
            }

            if (scrollbar != _scrollbarHover)
            {
                _scrollbarHover = scrollbar;
                Invalidate(new Rectangle(ClientSize.Width - 16, 0, 16, ClientSize.Height));
            }
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            InvalidateCard(_hoverIndex);
            _hoverIndex = -1;
            _deleteHover = false;
            if (_toolbarHover != -1)
            {
                _toolbarHover = -1;
                Invalidate(new Rectangle(0, 0, Width, _toolbarHeight));
            }
            if (_scrollbarHover)
            {
                _scrollbarHover = false;
                Invalidate(new Rectangle(ClientSize.Width - 16, 0, 16, ClientSize.Height));
            }
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _suppressClick = false;

            if (e.Button == MouseButtons.Left && HandleToolbarClick(e.Location))
            {
                _suppressClick = true;
                _mouseDownIndex = -1;
                _deletePressed = false;
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                _mouseDownIndex = -1;
                if (_selectionMode)
                {
                    return;
                }
                int menuIndex = HitTest(e.Location);
                if (menuIndex < 0)
                {
                    return;
                }
                _hoverIndex = menuIndex;
                _deleteHover = false;
                Invalidate();
                ShowItemMenu(menuIndex, e.Location);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (_selectionMode)
                {
                    _mouseDownIndex = -1;
                    _deletePressed = false;
                    return;
                }
                int index = HitTest(e.Location);
                _mouseDownIndex = index;
                _mouseDownPoint = e.Location;
                _deletePressed = IsDeleteButtonHit(index, e.Location);
                if (_deletePressed)
                {
                    _suppressClick = true;
                    InvalidateCard(index);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_selectionMode)
            {
                _deletePressed = false;
                _mouseDownIndex = -1;
                return;
            }

            int index = _mouseDownIndex;
            bool delete = _deletePressed && IsDeleteButtonHit(index, e.Location);
            _deletePressed = false;
            _mouseDownIndex = -1;
            InvalidateCard(index);

            if (delete && index >= 0)
            {
                _suppressClick = true;
                DeleteItem(index);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }
            if (e.Y < _toolbarHeight)
            {
                return;
            }
            int index = HitTest(e.Location);
            if (index < 0)
            {
                return;
            }
            if (_selectionMode)
            {
                ToggleSelected(index);
                return;
            }
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                EnterSelection();
                _selectedPaths.Add(_items[index].Shot.Path);
                Invalidate();
                return;
            }

            Action<ShotFile> handler = ShotActivated;
            if (handler != null)
            {
                handler(_items[index].Shot);
            }
        }

        private void BeginDrag(int index)
        {
            if (_dragging || index < 0 || index >= _items.Count)
            {
                return;
            }
            string path = _items[index].Shot.Path;
            if (!File.Exists(path))
            {
                return;
            }
            _dragging = true;
            _suppressClick = true;
            _mouseDownIndex = -1;
            _deletePressed = false;
            try
            {
                DataObject data = new DataObject();
                data.SetData(DataFormats.FileDrop, new string[] { path });
                DoDragDrop(data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                CaptureStore.Log("drag out failed: " + ex.Message);
            }
            finally
            {
                _dragging = false;
                Cursor = Cursors.Default;
            }
        }

        private void DeleteItem(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }
            GalleryItem item = _items[index];
            if (_mode == GalleryMode.Recycle)
            {
                PermanentDelete(item);
            }
            else
            {
                MoveToRecycle(item);
            }
        }

        private void MoveToRecycle(GalleryItem item)
        {
            string destination = CaptureStore.MoveToRecycle(item.Shot.Path);
            if (string.IsNullOrEmpty(destination))
            {
                NotifyStatus(Loc.T("移入回收站失败，请稍后重试。",
                    "Could not move it to the Recycle Bin. Try again."), true);
                return;
            }
            Reload(false);
            NotifyChanged();
            NotifyStatus(Loc.T("已移入回收站", "Moved to the Recycle Bin"), false);
        }

        private void PermanentDelete(GalleryItem item)
        {
            using (ConfirmDialog dialog = new ConfirmDialog(
                Loc.T("彻底删除这张截图？", "Delete this screenshot forever?"),
                Loc.T("此操作会从计算机永久删除图片，无法恢复。",
                    "This permanently removes the image from your computer."),
                Loc.T("彻底删除", "Delete Forever")))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || !dialog.Confirmed)
                {
                    return;
                }
            }

            if (!CaptureStore.DeletePermanently(item.Shot.Path))
            {
                NotifyStatus(Loc.T("彻底删除失败，请稍后重试。",
                    "Could not delete it permanently. Try again."), true);
                return;
            }
            Reload(false);
            NotifyChanged();
            NotifyStatus(Loc.T("已从计算机彻底删除", "Deleted permanently"), false);
        }

        private void RestoreItem(GalleryItem item)
        {
            string restored = CaptureStore.RestoreFromRecycle(item.Shot.Path);
            if (string.IsNullOrEmpty(restored))
            {
                NotifyStatus(Loc.T("还原失败，请稍后重试。", "Could not restore it. Try again."), true);
                return;
            }
            Reload(false);
            NotifyChanged();
            NotifyStatus(Loc.T("已还原到图库", "Restored to the library"), false);
        }

        private void NotifyChanged()
        {
            EventHandler handler = ItemsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void NotifyStatus(string message, bool error)
        {
            Action<string, bool> handler = StatusMessage;
            if (handler != null)
            {
                handler(message, error);
            }
        }

        private void ShowItemMenu(int index, Point location)
        {
            if (_menuOpen || index < 0 || index >= _items.Count)
            {
                return;
            }
            GalleryItem item = _items[index];
            ContextMenuStrip menu = UiTheme.CreateMenu();
            _menuOpen = true;
            try
            {
                ToolStripMenuItem open = UiTheme.MenuItem(Loc.T("预览", "Preview"));
                open.Font = UiTheme.UiFont(9.5f, FontStyle.Bold);
                open.Click += delegate
                {
                    Action<ShotFile> handler = ShotActivated;
                    if (handler != null)
                    {
                        handler(item.Shot);
                    }
                };

                ToolStripMenuItem copy = UiTheme.MenuItem(Loc.T("复制图片", "Copy Image"));
                copy.Click += delegate
                {
                    try
                    {
                        using (Bitmap full = CaptureStore.LoadFullImage(item.Shot.Path))
                        {
                            CaptureStore.CopyToClipboard(full);
                        }
                    }
                    catch (Exception ex)
                    {
                        CaptureStore.Log("copy failed: " + ex.Message);
                    }
                };

                menu.Items.Add(open);
                menu.Items.Add(copy);

                if (_mode == GalleryMode.Library)
                {
                    ToolStripMenuItem reveal = UiTheme.MenuItem(Loc.T("在文件夹中显示", "Show in Folder"));
                    reveal.Click += delegate { CaptureStore.RevealInExplorer(item.Shot.Path); };
                    ToolStripMenuItem recycle = UiTheme.MenuItem(Loc.T("移到回收站", "Move to Recycle Bin"));
                    recycle.Click += delegate { MoveToRecycle(item); };
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(reveal);
                    menu.Items.Add(recycle);
                }
                else
                {
                    ToolStripMenuItem restore = UiTheme.MenuItem(Loc.T("还原到图库", "Restore to Library"));
                    restore.Click += delegate { RestoreItem(item); };
                    ToolStripMenuItem delete = UiTheme.MenuItem(Loc.T("彻底删除", "Delete Forever"));
                    delete.Click += delegate { PermanentDelete(item); };
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(restore);
                    menu.Items.Add(delete);
                }

                menu.Closed += delegate
                {
                    _menuOpen = false;
                    menu.Dispose();
                };
                menu.Show(this, location);
            }
            catch
            {
                _menuOpen = false;
                menu.Dispose();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0)
            {
                return;
            }
            int notches = Math.Max(1, Math.Abs(e.Delta) / 120);
            int step = (int)(112 * _dpi) * notches;
            int next = ClampScroll(_scroll - Math.Sign(e.Delta) * step);
            if (next != _scroll)
            {
                _scroll = next;
                _hoverIndex = -1;
                _deleteHover = false;
                QueueVisibleThumbs();
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcLayout();
            _scroll = ClampScroll(_scroll);
            QueueVisibleThumbs();
            Invalidate();
        }

        private void QueueVisibleThumbs()
        {
            if (_items.Count == 0)
            {
                return;
            }
            int firstRow = Math.Max(0, _scroll / Math.Max(1, _cardHeight + _gap) - 1);
            int lastRow = firstRow + ClientSize.Height / Math.Max(1, _cardHeight + _gap) + 2;
            int start = firstRow * _columns;
            int end = Math.Min(_items.Count, (lastRow + 1) * _columns);
            for (int i = start; i < end; i++)
            {
                GalleryItem item = _items[i];
                if (item.Thumb == null && !item.Loading && !item.Failed)
                {
                    QueueThumb(item);
                }
            }
        }

        private void QueueThumb(GalleryItem item)
        {
            if (item.Loading || item.Thumb != null || item.Failed)
            {
                return;
            }
            item.Loading = true;
            int maxW = Math.Max(320, (int)(_cardWidth * 1.25));
            int maxH = Math.Max(200, (int)(_cardHeight * 1.25));
            ThreadPool.QueueUserWorkItem(delegate
            {
                ThumbResult result = null;
                bool entered = false;
                try
                {
                    _thumbGate.WaitOne();
                    entered = true;
                    result = CaptureStore.LoadThumbnail(item.Shot.Path, maxW, maxH);
                }
                catch (Exception ex)
                {
                    CaptureStore.Log("thumb load failed: " + ex.Message);
                    result = new ThumbResult();
                    result.Failed = true;
                }
                finally
                {
                    if (entered)
                    {
                        try { _thumbGate.Release(); } catch { }
                    }
                }

                if (IsDisposed)
                {
                    if (result != null && result.Image != null)
                    {
                        result.Image.Dispose();
                    }
                    return;
                }

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        item.Loading = false;
                        if (IsDisposed)
                        {
                            if (result != null && result.Image != null)
                            {
                                result.Image.Dispose();
                            }
                            return;
                        }
                        if (!_items.Contains(item))
                        {
                            if (result != null && result.Image != null)
                            {
                                result.Image.Dispose();
                            }
                            return;
                        }

                        item.Width = result != null ? result.Width : 0;
                        item.Height = result != null ? result.Height : 0;
                        item.Failed = result == null || result.Failed;
                        if (result != null && result.Image != null && !item.Failed)
                        {
                            item.Thumb = result.Image;
                        }
                        else if (result != null && result.Image != null)
                        {
                            result.Image.Dispose();
                        }

                        int idx = _items.IndexOf(item);
                        Rectangle rect = RectForIndex(idx, true);
                        if (!rect.IsEmpty)
                        {
                            Invalidate(Rectangle.Inflate(rect, 4, 4));
                        }
                    }));
                }
                catch
                {
                    if (result != null && result.Image != null)
                    {
                        result.Image.Dispose();
                    }
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Thumb != null)
                    {
                        _items[i].Thumb.Dispose();
                        _items[i].Thumb = null;
                    }
                }
                _timeFont.Dispose();
                _emptyTitleFont.Dispose();
                _emptySubFont.Dispose();
                _tinyFont.Dispose();
                _thumbGate.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
