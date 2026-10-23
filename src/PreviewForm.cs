using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class PreviewForm : Form
    {
        private enum PreviewAction
        {
            Previous,
            Next,
            Recycle,
            Restore,
            DeleteForever,
            Close
        }

        private sealed class PreviewButton
        {
            public PreviewAction Action;
            public Rectangle Rect;
            public string Text;
            public bool Danger;
            public bool Enabled;
        }

        private readonly List<ShotFile> _shots;
        private readonly List<PreviewButton> _buttons = new List<PreviewButton>();
        private readonly bool _recycleMode;
        private readonly Font _titleFont;
        private readonly Font _metaFont;
        private readonly Font _buttonFont;
        private readonly Font _hintFont;
        private readonly Font _errorFont;
        private readonly Timer _noticeTimer;
        private float _dpi = 1f;
        private int _index;
        private Bitmap _image;
        private string _error;
        private string _notice = string.Empty;
        private bool _noticeError;
        private Rectangle _imageRect;
        private Rectangle _closeRect;
        private Rectangle _barRect;
        private bool _hoverClose;
        private Point _dragStart;
        private bool _dragArmed;
        private bool _dragStarted;
        private bool _suppressClick;
        private PreviewAction _hoverAction;
        private bool _hasHoverAction;

        public PreviewForm(List<ShotFile> shots, int index, Rectangle screenBounds, Icon appIcon, bool recycleMode)
        {
            _shots = shots;
            _recycleMode = recycleMode;
            _index = Math.Max(0, Math.Min(shots.Count - 1, index));

            Text = recycleMode
                ? Loc.T("截一下预览 · 回收站", "JieYiXia Preview · Recycle Bin")
                : Loc.T("截一下预览", "JieYiXia Preview");
            Icon = appIcon;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            BackColor = Color.FromArgb(7, 8, 10);
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.Selectable, true);

            _titleFont = UiTheme.UiFont(14f, FontStyle.Bold);
            _metaFont = UiTheme.UiFont(9f, FontStyle.Regular);
            _buttonFont = UiTheme.UiFont(10.2f, FontStyle.Regular);
            _hintFont = UiTheme.UiFont(9.4f, FontStyle.Regular);
            _errorFont = UiTheme.UiFont(11f, FontStyle.Regular);
            _noticeTimer = new Timer();
            _noticeTimer.Interval = 1900;
            _noticeTimer.Tick += delegate
            {
                _noticeTimer.Stop();
                _notice = string.Empty;
                Invalidate();
            };

            LoadCurrent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (Graphics g = CreateGraphics())
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            RecalcHitRects();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcHitRects();
            Invalidate();
        }

        private void RecalcHitRects()
        {
            int margin = (int)(24 * _dpi);
            int size = (int)(40 * _dpi);
            _closeRect = new Rectangle(ClientSize.Width - margin - size, margin, size, size);
            RebuildButtons();
        }

        private void RebuildButtons()
        {
            _buttons.Clear();
            _hasHoverAction = false;
            AddButton(PreviewAction.Previous, Loc.T("上一张  ←", "Previous  ←"), false, _shots.Count > 1);
            AddButton(PreviewAction.Next, Loc.T("下一张  →", "Next  →"), false, _shots.Count > 1);
            if (_recycleMode)
            {
                AddButton(PreviewAction.Restore, Loc.T("还原", "Restore"), false, _shots.Count > 0);
                AddButton(PreviewAction.DeleteForever, Loc.T("彻底删除  Del", "Delete Forever  Del"),
                    true, _shots.Count > 0);
            }
            else
            {
                AddButton(PreviewAction.Recycle, Loc.T("移入回收站  Del", "Move to Recycle Bin  Del"),
                    false, _shots.Count > 0);
            }
            AddButton(PreviewAction.Close, Loc.T("关闭  Esc", "Close  Esc"), false, true);

            int gap = (int)(9 * _dpi);
            int pad = (int)(11 * _dpi);
            int total = pad * 2 + gap * Math.Max(0, _buttons.Count - 1);
            for (int i = 0; i < _buttons.Count; i++)
            {
                total += _buttons[i].Rect.Width;
            }
            int barHeight = (int)(64 * _dpi);
            int barTop = ClientSize.Height - (int)(22 * _dpi) - barHeight;
            int x = (ClientSize.Width - total) / 2;
            if (x < 10)
            {
                x = 10;
            }
            _barRect = new Rectangle(x, barTop, total, barHeight);
            x += pad;
            int y = barTop + (barHeight - (int)(42 * _dpi)) / 2;
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Rect = new Rectangle(x, y, _buttons[i].Rect.Width, (int)(42 * _dpi));
                x += _buttons[i].Rect.Width + gap;
            }
        }

        private void AddButton(PreviewAction action, string text, bool danger, bool enabled)
        {
            Size measured = TextRenderer.MeasureText(text, _buttonFont);
            int width = Math.Max((int)(84 * _dpi), measured.Width + (int)(30 * _dpi));
            PreviewButton button = new PreviewButton();
            button.Action = action;
            button.Text = text;
            button.Danger = danger;
            button.Enabled = enabled;
            button.Rect = new Rectangle(0, 0, width, (int)(38 * _dpi));
            _buttons.Add(button);
        }

        private void LoadCurrent()
        {
            if (_image != null)
            {
                _image.Dispose();
                _image = null;
            }
            _error = null;
            if (_shots.Count == 0)
            {
                _error = Loc.T("图库为空", "The library is empty");
                RebuildButtons();
                Invalidate();
                return;
            }
            if (_index < 0 || _index >= _shots.Count)
            {
                _index = 0;
            }
            try
            {
                _image = CaptureStore.LoadFullImage(_shots[_index].Path);
            }
            catch (Exception ex)
            {
                _error = Loc.T("无法读取这张截图", "Could not read this screenshot");
                CaptureStore.Log("preview load failed: " + ex.Message);
            }
            RebuildButtons();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (LinearGradientBrush bg = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(13, 14, 18),
                Color.FromArgb(5, 6, 8),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_image != null)
            {
                int left = (int)(88 * _dpi);
                int right = (int)(88 * _dpi);
                int top = (int)(96 * _dpi);
                int bottom = (int)(150 * _dpi);
                Rectangle area = Rectangle.FromLTRB(
                    left,
                    top,
                    Math.Max(left + 20, ClientSize.Width - right),
                    Math.Max(top + 20, ClientSize.Height - bottom));
                _imageRect = FitRect(_image.Width, _image.Height, area);
                g.DrawImage(_image, _imageRect);
                using (Pen border = new Pen(Color.FromArgb(36, 255, 255, 255)))
                {
                    g.DrawRectangle(border, _imageRect.Left - 1, _imageRect.Top - 1, _imageRect.Width + 1, _imageRect.Height + 1);
                }
            }
            else
            {
                _imageRect = Rectangle.Empty;
                using (SolidBrush b = new SolidBrush(UiTheme.Muted))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(_error ?? Loc.T("无法读取", "Unreadable"), _errorFont, b, ClientRectangle, sf);
                }
            }

            DrawTopInfo(g);
            DrawCloseButton(g);
            DrawControlBar(g);
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

        private void DrawTopInfo(Graphics g)
        {
            if (_shots.Count == 0)
            {
                return;
            }
            ShotFile shot = _shots[_index];
            float x = 32 * _dpi;
            float y = 26 * _dpi;
            using (SolidBrush text = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.LongDateTime(shot.Time), _titleFont, text, x, y);
            }
            string meta = (_image != null ? _image.Width + " × " + _image.Height + "   ·   " : string.Empty)
                + (_index + 1) + " / " + _shots.Count
                + (_recycleMode ? Loc.T("   ·   回收站", "   ·   Recycle Bin") : string.Empty);
            using (SolidBrush muted = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(meta, _metaFont, muted, x + 1, y + 31 * _dpi);
            }
        }

        private void DrawCloseButton(Graphics g)
        {
            using (SolidBrush fill = new SolidBrush(_hoverClose
                ? Color.FromArgb(46, 49, 58)
                : Color.FromArgb(22, 24, 30)))
            {
                g.FillEllipse(fill, _closeRect);
            }
            using (Pen border = new Pen(Color.FromArgb(_hoverClose ? 80 : 38, 255, 255, 255)))
            {
                g.DrawEllipse(border, _closeRect);
            }
            float cx = _closeRect.Left + _closeRect.Width / 2f;
            float cy = _closeRect.Top + _closeRect.Height / 2f;
            float d = _closeRect.Width * 0.19f;
            using (Pen pen = new Pen(UiTheme.Text, 1.8f))
            {
                g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                g.DrawLine(pen, cx + d, cy - d, cx - d, cy + d);
            }
        }

        private void DrawControlBar(Graphics g)
        {
            if (_barRect.IsEmpty)
            {
                return;
            }

            string hint = !string.IsNullOrEmpty(_notice)
                ? _notice
                : Loc.T("拖拽图片可直接发送到其他应用", "Drag the image to send it to another app");
            SizeF hintSize = g.MeasureString(hint, _hintFont);
            float hintX = (ClientSize.Width - hintSize.Width) / 2f;
            float hintY = _barRect.Top - hintSize.Height - 7 * _dpi;
            Color hintColor = !string.IsNullOrEmpty(_notice)
                ? (_noticeError ? Color.FromArgb(255, 130, 120) : Color.FromArgb(124, 217, 172))
                : Color.FromArgb(120, 125, 136);
            using (SolidBrush hb = new SolidBrush(hintColor))
            {
                g.DrawString(hint, _hintFont, hb, hintX, hintY);
            }

            using (GraphicsPath path = UiTheme.RoundedRect(_barRect, 13f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(238, 19, 21, 27)))
            using (Pen border = new Pen(Color.FromArgb(45, 49, 59)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                DrawButton(g, _buttons[i]);
            }
        }

        private void DrawButton(Graphics g, PreviewButton button)
        {
            bool hover = _hasHoverAction && _hoverAction == button.Action && button.Enabled;
            Color fill;
            Color border;
            Color text;
            if (button.Danger)
            {
                fill = hover ? Color.FromArgb(68, 37, 36) : Color.FromArgb(38, 27, 29);
                border = hover ? Color.FromArgb(199, 92, 84) : Color.FromArgb(82, 54, 54);
                text = button.Enabled ? Color.FromArgb(255, 143, 132) : UiTheme.Faint;
            }
            else
            {
                fill = hover ? Color.FromArgb(46, 49, 59) : Color.FromArgb(31, 34, 42);
                border = hover ? Color.FromArgb(85, 90, 103) : Color.FromArgb(53, 57, 67);
                text = button.Enabled ? UiTheme.Text : UiTheme.Faint;
            }

            using (GraphicsPath path = UiTheme.RoundedRect(button.Rect, 9f * _dpi))
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
                g.DrawString(button.Text, _buttonFont, tb, button.Rect, sf);
            }
        }

        private void Navigate(int delta)
        {
            if (_shots.Count <= 1)
            {
                return;
            }
            _index = (_index + delta + _shots.Count) % _shots.Count;
            LoadCurrent();
        }

        private void DeleteCurrent()
        {
            if (_shots.Count == 0 || _index < 0 || _index >= _shots.Count)
            {
                return;
            }
            ShotFile shot = _shots[_index];
            if (_recycleMode)
            {
                using (ConfirmDialog dialog = new ConfirmDialog(
                    Loc.T("彻底删除这张截图？", "Delete this screenshot forever?"),
                    Loc.T("此操作会从计算机永久删除图片，无法恢复。",
                        "This permanently removes the image from your computer."),
                    Loc.T("彻底删除", "Delete Forever")))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.Confirmed)
                    {
                        return;
                    }
                }
                if (!CaptureStore.DeletePermanently(shot.Path))
                {
                    ShowNotice(Loc.T("彻底删除失败", "Permanent delete failed"), true);
                    return;
                }
                RemoveCurrent(Loc.T("已从计算机彻底删除", "Deleted permanently"), false);
            }
            else
            {
                string destination = CaptureStore.MoveToRecycle(shot.Path);
                if (string.IsNullOrEmpty(destination))
                {
                    ShowNotice(Loc.T("移入回收站失败", "Could not move to the Recycle Bin"), true);
                    return;
                }
                RemoveCurrent(Loc.T("已移入回收站", "Moved to the Recycle Bin"), false);
            }
        }

        private void RestoreCurrent()
        {
            if (_shots.Count == 0 || _index < 0 || _index >= _shots.Count)
            {
                return;
            }
            string restored = CaptureStore.RestoreFromRecycle(_shots[_index].Path);
            if (string.IsNullOrEmpty(restored))
            {
                ShowNotice(Loc.T("还原失败", "Restore failed"), true);
                return;
            }
            RemoveCurrent(Loc.T("已还原到图库", "Restored to the library"), false);
        }

        private void RemoveCurrent(string notice, bool error)
        {
            if (_shots.Count == 0 || _index < 0 || _index >= _shots.Count)
            {
                return;
            }
            _shots.RemoveAt(_index);
            if (_shots.Count == 0)
            {
                Close();
                return;
            }
            if (_index >= _shots.Count)
            {
                _index = _shots.Count - 1;
            }
            LoadCurrent();
            ShowNotice(notice, error);
        }

        private void ShowNotice(string message, bool error)
        {
            _notice = message;
            _noticeError = error;
            _noticeTimer.Stop();
            _noticeTimer.Start();
            Invalidate();
        }

        private void Execute(PreviewAction action)
        {
            switch (action)
            {
                case PreviewAction.Previous:
                    Navigate(-1);
                    break;
                case PreviewAction.Next:
                    Navigate(1);
                    break;
                case PreviewAction.Recycle:
                    DeleteCurrent();
                    break;
                case PreviewAction.Restore:
                    RestoreCurrent();
                    break;
                case PreviewAction.DeleteForever:
                    DeleteCurrent();
                    break;
                case PreviewAction.Close:
                    Close();
                    break;
            }
        }

        private PreviewButton ButtonAt(Point point)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Rect.Contains(point))
                {
                    return _buttons[i];
                }
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragArmed && (Control.MouseButtons & MouseButtons.Left) != 0 && _image != null)
            {
                int dx = Math.Abs(e.X - _dragStart.X);
                int dy = Math.Abs(e.Y - _dragStart.Y);
                if (dx > 6 || dy > 6)
                {
                    BeginDrag();
                    return;
                }
            }

            bool close = _closeRect.Contains(e.Location);
            PreviewButton button = ButtonAt(e.Location);
            bool hasAction = button != null && button.Enabled;
            PreviewAction action = hasAction ? button.Action : PreviewAction.Previous;
            if (close != _hoverClose || hasAction != _hasHoverAction || (hasAction && action != _hoverAction))
            {
                _hoverClose = close;
                _hasHoverAction = hasAction;
                if (hasAction)
                {
                    _hoverAction = action;
                }
                Cursor = close || hasAction ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        private void BeginDrag()
        {
            if (_dragStarted || _image == null || _shots.Count == 0)
            {
                return;
            }
            string path = _shots[_index].Path;
            if (!File.Exists(path))
            {
                return;
            }
            _dragStarted = true;
            _dragArmed = false;
            _suppressClick = true;
            try
            {
                DataObject data = new DataObject();
                data.SetData(DataFormats.FileDrop, new string[] { path });
                DoDragDrop(data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                CaptureStore.Log("preview drag failed: " + ex.Message);
            }
            finally
            {
                _dragStarted = false;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            _suppressClick = false;
            if (_imageRect.Contains(e.Location) && ButtonAt(e.Location) == null && !_closeRect.Contains(e.Location))
            {
                _dragStart = e.Location;
                _dragArmed = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragArmed = false;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right)
            {
                Close();
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }
            if (_closeRect.Contains(e.Location))
            {
                Close();
                return;
            }
            PreviewButton button = ButtonAt(e.Location);
            if (button != null && button.Enabled)
            {
                Execute(button.Action);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Navigate(e.Delta > 0 ? -1 : 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Close();
                    break;
                case Keys.Left:
                    Navigate(-1);
                    break;
                case Keys.Right:
                    Navigate(1);
                    break;
                case Keys.Home:
                    if (_shots.Count > 0)
                    {
                        _index = 0;
                        LoadCurrent();
                    }
                    break;
                case Keys.End:
                    if (_shots.Count > 0)
                    {
                        _index = _shots.Count - 1;
                        LoadCurrent();
                    }
                    break;
                case Keys.Delete:
                    DeleteCurrent();
                    break;
                case Keys.R:
                    if (_recycleMode)
                    {
                        RestoreCurrent();
                    }
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _noticeTimer.Stop();
                _noticeTimer.Dispose();
                if (_image != null)
                {
                    _image.Dispose();
                    _image = null;
                }
                _titleFont.Dispose();
                _metaFont.Dispose();
                _buttonFont.Dispose();
                _hintFont.Dispose();
                _errorFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
