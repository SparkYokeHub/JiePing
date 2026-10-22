using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class UpdatePage : Control
    {
        private enum PageState
        {
            Idle,
            Checking,
            Latest,
            Found,
            Downloading,
            Downloaded,
            Error
        }

        private readonly Font _titleFont;
        private readonly Font _subFont;
        private readonly Font _cardTitleFont;
        private readonly Font _bodyFont;
        private readonly Font _smallFont;
        private readonly Font _bigFont;
        private readonly TextBox _urlBox;

        private float _dpi = 1f;
        private Rectangle _backRect;
        private Rectangle _noticeRect;
        private Rectangle _cardRect;
        private Rectangle _checkRect;
        private Rectangle _downloadRect;
        private Rectangle _installRect;
        private Rectangle _openFolderRect;
        private Rectangle _saveUrlRect;
        private Rectangle _progressRect;
        private bool _hoverBack;
        private bool _hoverCheck;
        private bool _hoverDownload;
        private bool _hoverInstall;
        private bool _hoverOpenFolder;
        private bool _hoverSaveUrl;
        private PageState _state = PageState.Idle;
        private UpdateManifest _manifest;
        private string _message = string.Empty;
        private bool _messageError;
        private int _progress;
        private string _downloadedFile;
        private bool _busy;

        public event EventHandler BackRequested;

        public UpdatePage()
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
            _smallFont = UiTheme.UiFont(8.8f, FontStyle.Regular);
            _bigFont = UiTheme.UiFont(16f, FontStyle.Bold);

            _urlBox = new TextBox();
            _urlBox.Font = UiTheme.UiFont(9f, FontStyle.Regular);
            _urlBox.BackColor = Color.FromArgb(17, 19, 24);
            _urlBox.ForeColor = UiTheme.Text;
            _urlBox.BorderStyle = BorderStyle.FixedSingle;
            _urlBox.Text = SettingsStore.GetUpdateUrl();
            _urlBox.MaxLength = 300;
            Controls.Add(_urlBox);

            UpdateService.SetBaseUrl(_urlBox.Text);
            _message = Loc.T("点击「检查更新」获取最新版本。", "Click Check for Updates to look for a new version.");
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
            int width = ClientSize.Width - left * 2;
            _noticeRect = new Rectangle(left, (int)(140 * _dpi), width, (int)(52 * _dpi));
            _cardRect = new Rectangle(left, (int)(210 * _dpi), width, (int)(430 * _dpi));
            _checkRect = new Rectangle(_cardRect.Right - (int)(142 * _dpi),
                _cardRect.Top + (int)(20 * _dpi), (int)(118 * _dpi), (int)(36 * _dpi));
            _downloadRect = new Rectangle(_cardRect.Right - (int)(154 * _dpi),
                _cardRect.Bottom - (int)(58 * _dpi), (int)(130 * _dpi), (int)(38 * _dpi));
            _installRect = new Rectangle(_cardRect.Right - (int)(154 * _dpi),
                _cardRect.Bottom - (int)(58 * _dpi), (int)(130 * _dpi), (int)(38 * _dpi));
            _openFolderRect = new Rectangle(_installRect.Left - (int)(128 * _dpi),
                _installRect.Top, (int)(118 * _dpi), (int)(38 * _dpi));
            _progressRect = new Rectangle(_cardRect.Left + (int)(24 * _dpi),
                _cardRect.Top + (int)(176 * _dpi), _cardRect.Width - (int)(48 * _dpi),
                (int)(10 * _dpi));
            _saveUrlRect = new Rectangle(ClientSize.Width - left - (int)(92 * _dpi),
                (int)(664 * _dpi), (int)(92 * _dpi), (int)(32 * _dpi));

            _urlBox.Location = new Point(left + (int)(130 * _dpi), (int)(666 * _dpi));
            _urlBox.Width = Math.Max(180, ClientSize.Width - left - (int)(92 * _dpi) - _urlBox.Left - (int)(12 * _dpi));
            _urlBox.Height = (int)(26 * _dpi);
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
                g.DrawString(Loc.T("软件升级", "Software Update"), _titleFont, title,
                    (int)(30 * _dpi), (int)(58 * _dpi));
            }
            using (SolidBrush sub = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("在软件内检查并下载新版本，不会跳转到浏览器。",
                        "Check and download new versions inside the app, without opening a browser."),
                    _subFont, sub, (int)(33 * _dpi), (int)(105 * _dpi));
            }

            DrawNotice(g);
            DrawCard(g);
            DrawUrlRow(g);
        }

        private void DrawNotice(Graphics g)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(_noticeRect, 11f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(42, 20, 21)))
            using (Pen border = new Pen(Color.FromArgb(112, 48, 43)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush text = new SolidBrush(Color.FromArgb(255, 112, 101)))
            {
                g.DrawString(Loc.T("此功能需联网，断网时无法检查或收到更新。",
                        "Internet required. Updates cannot be checked or received offline."),
                    _bodyFont, text,
                    new RectangleF(_noticeRect.Left + (int)(20 * _dpi),
                        _noticeRect.Top + (int)(16 * _dpi),
                        _noticeRect.Width - (int)(40 * _dpi),
                        _noticeRect.Height - (int)(20 * _dpi)));
            }
        }

        private void DrawCard(Graphics g)
        {
            DrawCardBackground(g, _cardRect);
            using (SolidBrush tb = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("当前版本 ", "Current version ") + UpdateService.CurrentVersion,
                    _cardTitleFont, tb, _cardRect.Left + (int)(24 * _dpi),
                    _cardRect.Top + (int)(20 * _dpi));
            }
            using (SolidBrush mb = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("点击检查更新；检测到新版本后可直接下载并替换当前程序。",
                        "Check for updates; when found, download and replace the current app."),
                    _smallFont, mb, _cardRect.Left + (int)(24 * _dpi),
                    _cardRect.Top + (int)(48 * _dpi));
            }

            DrawButton(g, _checkRect, Loc.T("检查更新", "Check for Updates"), true,
                _hoverCheck, !_busy);

            if (_state == PageState.Idle || _state == PageState.Checking ||
                _state == PageState.Latest || _state == PageState.Error)
            {
                Color color = _messageError ? Color.FromArgb(255, 116, 106) : UiTheme.Muted;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.DrawString(_message, _bodyFont, b,
                        new RectangleF(_cardRect.Left + (int)(24 * _dpi),
                            _cardRect.Top + (int)(88 * _dpi),
                            _cardRect.Width - (int)(48 * _dpi),
                            (int)(70 * _dpi)));
                }
                if (_state == PageState.Checking)
                {
                    DrawSpinner(g);
                }
            }
            else if (_state == PageState.Found)
            {
                DrawFound(g);
            }
            else if (_state == PageState.Downloading)
            {
                DrawDownloading(g);
            }
            else if (_state == PageState.Downloaded)
            {
                DrawDownloaded(g);
            }
        }

        private void DrawFound(Graphics g)
        {
            using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
            {
                g.DrawString(Loc.T("发现新版本 ", "New version ") + _manifest.Version,
                    _bigFont, accent, _cardRect.Left + (int)(24 * _dpi),
                    _cardRect.Top + (int)(82 * _dpi));
            }
            string size = FormatSize(_manifest.Size);
            string published = string.IsNullOrEmpty(_manifest.Published)
                ? string.Empty
                : Loc.T("  ·  发布 ", "  ·  published ") + _manifest.Published;
            using (SolidBrush muted = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(size + published, _smallFont, muted,
                    _cardRect.Left + (int)(24 * _dpi), _cardRect.Top + (int)(118 * _dpi));
            }
            using (SolidBrush body = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(string.IsNullOrEmpty(_manifest.Notes)
                        ? Loc.T("暂无更新说明。", "No release notes.")
                        : _manifest.Notes,
                    _bodyFont, body,
                    new RectangleF(_cardRect.Left + (int)(24 * _dpi),
                        _cardRect.Top + (int)(150 * _dpi),
                        _cardRect.Width - (int)(48 * _dpi),
                        (int)(170 * _dpi)));
            }
            DrawButton(g, _downloadRect, Loc.T("下载更新", "Download"), true,
                _hoverDownload, !_busy);
        }

        private void DrawDownloading(Graphics g)
        {
            using (SolidBrush tb = new SolidBrush(UiTheme.Text))
            {
                g.DrawString(Loc.T("正在下载更新…", "Downloading update..."), _cardTitleFont, tb,
                    _cardRect.Left + (int)(24 * _dpi), _cardRect.Top + (int)(92 * _dpi));
            }
            using (SolidBrush mb = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(_progress + "%", _bigFont, mb,
                    _cardRect.Left + (int)(24 * _dpi), _cardRect.Top + (int)(122 * _dpi));
            }
            using (GraphicsPath path = UiTheme.RoundedRect(_progressRect, 5f * _dpi))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(35, 38, 46)))
            {
                g.FillPath(fill, path);
            }
            int width = (int)(_progressRect.Width * (_progress / 100f));
            if (width > 1)
            {
                Rectangle done = new Rectangle(_progressRect.Left, _progressRect.Top,
                    Math.Max((int)(8 * _dpi), width), _progressRect.Height);
                using (GraphicsPath path = UiTheme.RoundedRect(done, 5f * _dpi))
                using (SolidBrush fill = new SolidBrush(UiTheme.Accent))
                {
                    g.FillPath(fill, path);
                }
            }
            using (SolidBrush muted = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("下载完成后会校验安装包，再提示立即更新。",
                        "The package is verified after download, then you can install it."),
                    _smallFont, muted, _cardRect.Left + (int)(24 * _dpi),
                    _cardRect.Top + (int)(210 * _dpi));
            }
        }

        private void DrawDownloaded(Graphics g)
        {
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(112, 211, 166)))
            {
                g.DrawString(Loc.T("更新包已下载并通过校验", "Update downloaded and verified"),
                    _cardTitleFont, tb, _cardRect.Left + (int)(24 * _dpi),
                    _cardRect.Top + (int)(92 * _dpi));
            }
            using (SolidBrush mb = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("文件：", "File: ") + _downloadedFile, _smallFont, mb,
                    new RectangleF(_cardRect.Left + (int)(24 * _dpi),
                        _cardRect.Top + (int)(124 * _dpi),
                        _cardRect.Width - (int)(48 * _dpi), (int)(40 * _dpi)));
            }
            using (SolidBrush nb = new SolidBrush(Color.FromArgb(255, 112, 101)))
            {
                g.DrawString(Loc.T("点击「立即更新」后程序会退出，替换为新版本并重新启动。",
                        "Install closes the app, replaces it with the new version and restarts."),
                    _smallFont, nb,
                    new RectangleF(_cardRect.Left + (int)(24 * _dpi),
                        _cardRect.Top + (int)(176 * _dpi),
                        _cardRect.Width - (int)(48 * _dpi), (int)(50 * _dpi)));
            }
            DrawButton(g, _openFolderRect, Loc.T("打开文件夹", "Open Folder"), false,
                _hoverOpenFolder, true);
            DrawButton(g, _installRect, Loc.T("立即更新", "Install Now"), true,
                _hoverInstall, !_busy);
        }

        private void DrawUrlRow(Graphics g)
        {
            using (SolidBrush label = new SolidBrush(UiTheme.Muted))
            {
                g.DrawString(Loc.T("更新清单地址", "Update manifest URL"), _smallFont, label,
                    (int)(42 * _dpi), (int)(672 * _dpi));
            }
            DrawButton(g, _saveUrlRect, Loc.T("保存", "Save"), false, _hoverSaveUrl, true);
            using (SolidBrush hint = new SolidBrush(UiTheme.Faint))
            {
                g.DrawString(Loc.T("例如 GitHub Releases 的 update.txt 地址",
                        "e.g. the update.txt asset on GitHub Releases"),
                    _smallFont, hint, (int)(42 * _dpi), (int)(704 * _dpi));
            }
        }

        private void DrawSpinner(Graphics g)
        {
            Rectangle r = new Rectangle(_cardRect.Left + (int)(24 * _dpi),
                _cardRect.Top + (int)(166 * _dpi), (int)(18 * _dpi), (int)(18 * _dpi));
            using (Pen pen = new Pen(UiTheme.Faint, 2f * _dpi))
            {
                g.DrawArc(pen, r, 30, 280);
            }
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

        private static void DrawCardBackground(Graphics g, Rectangle rect)
        {
            using (GraphicsPath path = UiTheme.RoundedRect(rect, 14f))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 22, 28)))
            using (Pen border = new Pen(Color.FromArgb(44, 48, 58)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        private void DrawButton(Graphics g, Rectangle rect, string text, bool primary, bool hover, bool enabled)
        {
            Color fill;
            Color border;
            Color textColor;
            if (!enabled)
            {
                fill = Color.FromArgb(24, 27, 33);
                border = Color.FromArgb(39, 43, 51);
                textColor = UiTheme.Faint;
            }
            else if (primary)
            {
                fill = hover ? Color.FromArgb(255, 136, 108) : UiTheme.Accent;
                border = Color.FromArgb(255, 154, 130);
                textColor = Color.FromArgb(38, 20, 15);
            }
            else
            {
                fill = hover ? Color.FromArgb(42, 46, 56) : Color.FromArgb(27, 30, 37);
                border = Color.FromArgb(hover ? 88 : 54, 255, 255, 255);
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
                g.DrawString(text, _bodyFont, tb, rect, sf);
            }
        }

        private static string FormatSize(long size)
        {
            if (size <= 0)
            {
                return Loc.T("大小未知", "Unknown size");
            }
            if (size >= 1024L * 1024L)
            {
                return (size / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            }
            return (size / 1024.0).ToString("0.0") + " KB";
        }

        private void CheckUpdates()
        {
            if (_busy) return;
            _busy = true;
            _state = PageState.Checking;
            _message = Loc.T("正在连接更新清单…", "Connecting to the update manifest...");
            _messageError = false;
            Invalidate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    UpdateManifest manifest = UpdateService.Check();
                    BeginInvoke(new Action(delegate
                    {
                        _manifest = manifest;
                        _busy = false;
                        if (UpdateService.IsNewer(manifest.Version))
                        {
                            _state = PageState.Found;
                        }
                        else
                        {
                            _state = PageState.Latest;
                            _message = Loc.T("已是最新版本（", "You are up to date (") +
                                UpdateService.CurrentVersion + ").";
                        }
                        Invalidate();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        _busy = false;
                        _state = PageState.Error;
                        _message = Loc.T("检查更新失败：", "Update check failed: ") + ex.Message;
                        _messageError = true;
                        Invalidate();
                    }));
                }
            });
        }

        private void DownloadUpdate()
        {
            if (_busy || _manifest == null) return;
            _busy = true;
            _state = PageState.Downloading;
            _progress = 0;
            Invalidate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string file = UpdateService.Download(_manifest, delegate(int percent)
                    {
                        try
                        {
                            BeginInvoke(new Action(delegate
                            {
                                _progress = percent;
                                Invalidate();
                            }));
                        }
                        catch
                        {
                        }
                    });
                    BeginInvoke(new Action(delegate
                    {
                        _downloadedFile = file;
                        _busy = false;
                        _state = PageState.Downloaded;
                        Invalidate();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        _busy = false;
                        _state = PageState.Error;
                        _message = Loc.T("下载失败：", "Download failed: ") + ex.Message;
                        _messageError = true;
                        Invalidate();
                    }));
                }
            });
        }

        private void InstallUpdate()
        {
            if (_busy || string.IsNullOrEmpty(_downloadedFile)) return;
            using (ConfirmDialog dialog = new ConfirmDialog(
                Loc.T("立即更新？", "Install the update now?"),
                Loc.T("程序会退出，替换为新版本后重新启动。", "The app will close, be replaced and restarted."),
                Loc.T("更新", "Install")))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || !dialog.Confirmed)
                {
                    return;
                }
            }
            string error;
            UpdateService.StartSelfUpdate(_downloadedFile, out error);
            if (!string.IsNullOrEmpty(error))
            {
                _state = PageState.Error;
                _message = Loc.T("无法启动更新：", "Could not start the update: ") + error;
                _messageError = true;
                Invalidate();
                return;
            }
            MainForm main = FindForm() as MainForm;
            if (main != null)
            {
                main.RequestExit();
            }
        }

        private void SaveUrl()
        {
            string url = (_urlBox.Text ?? string.Empty).Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _message = Loc.T("更新清单地址必须以 http:// 或 https:// 开头。",
                    "The manifest URL must start with http:// or https://.");
                _messageError = true;
                _state = PageState.Error;
                Invalidate();
                return;
            }
            string error;
            if (!SettingsStore.SaveUpdateUrl(url, out error))
            {
                _message = Loc.T("保存失败：", "Save failed: ") + error;
                _messageError = true;
                _state = PageState.Error;
                Invalidate();
                return;
            }
            UpdateService.SetManifestUrl(url);
            _message = Loc.T("更新清单地址已保存。", "Update manifest URL saved.");
            _messageError = false;
            _state = PageState.Idle;
            Invalidate();
        }

        private void OpenDownloadFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(_downloadedFile) && File.Exists(_downloadedFile))
                {
                    Process.Start("explorer.exe", "/select,\"" + _downloadedFile + "\"");
                }
            }
            catch
            {
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool back = _backRect.Contains(e.Location);
            bool check = _checkRect.Contains(e.Location);
            bool download = _downloadRect.Contains(e.Location);
            bool install = _installRect.Contains(e.Location);
            bool folder = _openFolderRect.Contains(e.Location);
            bool save = _saveUrlRect.Contains(e.Location);
            if (back != _hoverBack || check != _hoverCheck || download != _hoverDownload ||
                install != _hoverInstall || folder != _hoverOpenFolder || save != _hoverSaveUrl)
            {
                _hoverBack = back;
                _hoverCheck = check;
                _hoverDownload = download;
                _hoverInstall = install;
                _hoverOpenFolder = folder;
                _hoverSaveUrl = save;
                Cursor = back || check || download || install || folder || save
                    ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverBack = _hoverCheck = _hoverDownload = _hoverInstall = false;
            _hoverOpenFolder = _hoverSaveUrl = false;
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
                if (handler != null) handler(this, EventArgs.Empty);
            }
            else if (_checkRect.Contains(e.Location))
            {
                CheckUpdates();
            }
            else if (_state == PageState.Found && _downloadRect.Contains(e.Location))
            {
                DownloadUpdate();
            }
            else if (_state == PageState.Downloaded && _installRect.Contains(e.Location))
            {
                InstallUpdate();
            }
            else if (_state == PageState.Downloaded && _openFolderRect.Contains(e.Location))
            {
                OpenDownloadFolder();
            }
            else if (_saveUrlRect.Contains(e.Location))
            {
                SaveUrl();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subFont.Dispose();
                _cardTitleFont.Dispose();
                _bodyFont.Dispose();
                _smallFont.Dispose();
                _bigFont.Dispose();
                _urlBox.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
