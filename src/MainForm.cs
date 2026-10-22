using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class MainForm : Form
    {
        private readonly SidebarPanel _sidebar;
        private readonly Panel _content;
        private readonly GalleryPanel _gallery;
        private readonly RecyclePage _recyclePage;
        private readonly SettingsPanel _settingsPanel;
        private readonly RewardPage _rewardPage;
        private readonly UpdatePage _updatePage;
        private readonly AboutPage _aboutPage;
        private readonly ToastPanel _toast;
        private readonly float _dpi;
        private AppView _currentView = AppView.Gallery;
        private bool _allowClose;
        private bool _hidePending;
        private bool _notifiedHidden;
        private uint _commandMessage;

        public event EventHandler HiddenToTray;
        public event EventHandler HotkeyChanged;
        public event EventHandler LanguageChanged;
        public event EventHandler ExitRequested;
        public event Action<int> CommandReceived;

        public MainForm(Icon appIcon)
        {
            Text = Loc.T("截屏", "JiePing");
            Icon = appIcon;
            BackColor = UiTheme.Background;
            ForeColor = UiTheme.Text;
            Font = UiTheme.UiFont(9f, FontStyle.Regular);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                _dpi = Math.Max(1f, g.DpiX / 96f);
            }
            MinimumSize = new Size((int)(920 * _dpi), (int)(560 * _dpi));

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            int targetW = Math.Min((int)(1320 * _dpi), (int)(area.Width * 0.86));
            int targetH = Math.Min((int)(860 * _dpi), (int)(area.Height * 0.86));
            targetW = Math.Max((int)(920 * _dpi), targetW);
            targetH = Math.Max((int)(560 * _dpi), targetH);
            Bounds = new Rectangle(
                area.Left + (area.Width - targetW) / 2,
                area.Top + (area.Height - targetH) / 2,
                targetW,
                targetH);

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = UiTheme.Background;

            _gallery = new GalleryPanel(GalleryMode.Library);
            _gallery.Dock = DockStyle.Fill;
            _gallery.ShotActivated += GalleryShotActivated;
            _gallery.ItemsChanged += LibraryItemsChanged;
            _gallery.StatusMessage += ShowToast;

            _settingsPanel = new SettingsPanel();
            _settingsPanel.Dock = DockStyle.Fill;
            _settingsPanel.Visible = false;
            _settingsPanel.BackRequested += delegate { ShowView(AppView.Gallery); };
            _settingsPanel.FolderChanged += StorageFolderChanged;
            _settingsPanel.HotkeyChanged += delegate
            {
                UpdateHotkeyUi();
                EventHandler handler = HotkeyChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            _settingsPanel.LanguageChanged += delegate { ApplyLanguage(); };

            _recyclePage = new RecyclePage();
            _recyclePage.Dock = DockStyle.Fill;
            _recyclePage.Visible = false;
            _recyclePage.BackRequested += delegate { ShowView(AppView.Gallery); };
            _recyclePage.Grid.ShotActivated += RecycleShotActivated;
            _recyclePage.Grid.ItemsChanged += RecycleItemsChanged;
            _recyclePage.Grid.StatusMessage += ShowToast;

            _rewardPage = new RewardPage();
            _rewardPage.Dock = DockStyle.Fill;
            _rewardPage.Visible = false;
            _rewardPage.BackRequested += delegate { ShowView(AppView.Gallery); };

            _updatePage = new UpdatePage();
            _updatePage.Dock = DockStyle.Fill;
            _updatePage.Visible = false;
            _updatePage.BackRequested += delegate { ShowView(AppView.Gallery); };

            _aboutPage = new AboutPage();
            _aboutPage.Dock = DockStyle.Fill;
            _aboutPage.Visible = false;
            _aboutPage.BackRequested += delegate { ShowView(AppView.Gallery); };

            _content.Controls.Add(_gallery);
            _content.Controls.Add(_settingsPanel);
            _content.Controls.Add(_recyclePage);
            _content.Controls.Add(_rewardPage);
            _content.Controls.Add(_updatePage);
            _content.Controls.Add(_aboutPage);

            _toast = new ToastPanel();
            _content.Controls.Add(_toast);

            _sidebar = new SidebarPanel(appIcon);
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = (int)(204 * _dpi);
            _sidebar.GalleryRequested += delegate { ShowView(AppView.Gallery); };
            _sidebar.SettingsRequested += delegate { ShowView(AppView.Settings); };
            _sidebar.RecycleRequested += delegate { ShowView(AppView.Recycle); };
            _sidebar.RewardRequested += delegate { ShowView(AppView.Reward); };
            _sidebar.UpdateRequested += delegate { ShowView(AppView.Update); };
            _sidebar.AboutRequested += delegate { ShowView(AppView.About); };
            _sidebar.ToggleRequested += delegate
            {
                bool collapsed = !_sidebar.Collapsed;
                _sidebar.Collapsed = collapsed;
                _sidebar.Width = (int)((collapsed ? 64 : 204) * _dpi);
            };

            Controls.Add(_content);
            Controls.Add(_sidebar);

            UpdateRecycleCount();
            UpdateHotkeyUi();
        }

        private void UpdateHotkeyUi()
        {
            HotkeyConfig config = SettingsStore.GetHotkey();
            _gallery.HotkeyText = config.DisplayText;
            _recyclePage.Grid.HotkeyText = config.DisplayText;
            _sidebar.HotkeyText = config.DisplayText;
        }

        private void ApplyLanguage()
        {
            Text = Loc.T("截屏", "JiePing");
            UpdateHotkeyUi();
            _sidebar.Invalidate();
            _gallery.Invalidate();
            _settingsPanel.Invalidate();
            _recyclePage.Invalidate();
            _recyclePage.Grid.Invalidate();
            _rewardPage.Invalidate();
            _updatePage.Invalidate();
            _aboutPage.Invalidate();
            _toast.Visible = false;
            EventHandler handler = LanguageChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public void SetCommandMessage(uint message)
        {
            _commandMessage = message;
        }

        protected override void WndProc(ref Message m)
        {
            if (_commandMessage != 0 && (uint)m.Msg == _commandMessage)
            {
                int command = m.WParam.ToInt32();
                Action<int> handler = CommandReceived;
                if (handler != null)
                {
                    BeginInvoke(new Action(delegate { handler(command); }));
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.EnableDarkTitleBar(Handle);
            PositionToast();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.EnableDarkTitleBar(Handle);
            FocusCurrentView();
        }

        public void ShowGallery()
        {
            ShowWindow();
            _gallery.CancelSelection();
            ShowView(AppView.Gallery);
        }

        private void ShowWindow()
        {
            ShowInTaskbar = true;
            if (!Visible)
            {
                Show();
            }
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            RefreshCurrentView();
            Native.ForceForeground(Handle);
            Activate();
            BringToFront();
            FocusCurrentView();
        }

        private void RefreshCurrentView()
        {
            if (_currentView == AppView.Gallery)
            {
                _gallery.RefreshIfNeeded();
            }
            else if (_currentView == AppView.Recycle)
            {
                _recyclePage.RefreshData();
                UpdateRecycleCount();
            }
            else if (_currentView == AppView.Settings)
            {
                _settingsPanel.RefreshPath();
            }
        }

        private void ShowView(AppView view)
        {
            if (view != AppView.Gallery)
            {
                _gallery.CancelSelection();
            }
            if (view != AppView.Recycle)
            {
                _recyclePage.Grid.CancelSelection();
            }
            _currentView = view;
            _gallery.Visible = view == AppView.Gallery;
            _settingsPanel.Visible = view == AppView.Settings;
            _recyclePage.Visible = view == AppView.Recycle;
            _rewardPage.Visible = view == AppView.Reward;
            _updatePage.Visible = view == AppView.Update;
            _aboutPage.Visible = view == AppView.About;
            _sidebar.SelectedView = view;

            if (view == AppView.Gallery)
            {
                _gallery.BringToFront();
                _gallery.RefreshIfNeeded();
                _gallery.Focus();
            }
            else if (view == AppView.Recycle)
            {
                _recyclePage.BringToFront();
                _recyclePage.RefreshData();
                UpdateRecycleCount();
                _recyclePage.Grid.Focus();
            }
            else if (view == AppView.Settings)
            {
                _settingsPanel.BringToFront();
                _settingsPanel.RefreshPath();
                _settingsPanel.Focus();
            }
            else if (view == AppView.Reward)
            {
                _rewardPage.BringToFront();
                _rewardPage.Focus();
            }
            else if (view == AppView.Update)
            {
                _updatePage.BringToFront();
                _updatePage.Focus();
            }
            else
            {
                _aboutPage.BringToFront();
                _aboutPage.Focus();
            }

            if (_toast.Visible)
            {
                _toast.BringToFront();
            }
        }

        private void FocusCurrentView()
        {
            if (_currentView == AppView.Gallery)
            {
                _gallery.Focus();
            }
            else if (_currentView == AppView.Recycle)
            {
                _recyclePage.Grid.Focus();
            }
            else if (_currentView == AppView.Settings)
            {
                _settingsPanel.Focus();
            }
            else if (_currentView == AppView.Reward)
            {
                _rewardPage.Focus();
            }
            else if (_currentView == AppView.Update)
            {
                _updatePage.Focus();
            }
            else
            {
                _aboutPage.Focus();
            }
        }

        public void HideToTray(bool notify)
        {
            if (!Visible)
            {
                return;
            }
            _hidePending = true;
            if (WindowState != FormWindowState.Normal)
            {
                WindowState = FormWindowState.Normal;
            }
            Hide();
            ShowInTaskbar = false;
            _hidePending = false;

            if (notify && !_notifiedHidden)
            {
                _notifiedHidden = true;
                EventHandler handler = HiddenToTray;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        public void RefreshGallery()
        {
            _gallery.MarkDirty();
            if (_gallery.Visible)
            {
                _gallery.Reload(true);
            }
            _recyclePage.Grid.MarkDirty();
            UpdateRecycleCount();
        }

        private void UpdateRecycleCount()
        {
            _sidebar.RecycleCount = CaptureStore.CountRecycled();
        }

        private void LibraryItemsChanged(object sender, EventArgs e)
        {
            UpdateRecycleCount();
            _recyclePage.Grid.MarkDirty();
        }

        private void RecycleItemsChanged(object sender, EventArgs e)
        {
            UpdateRecycleCount();
            _gallery.MarkDirty();
        }

        private void StorageFolderChanged(object sender, EventArgs e)
        {
            _gallery.MarkDirty();
            _recyclePage.Grid.MarkDirty();
            if (_gallery.Visible)
            {
                _gallery.Reload(true);
            }
            if (_recyclePage.Visible)
            {
                _recyclePage.RefreshData();
            }
            _settingsPanel.RefreshPath();
            UpdateRecycleCount();
            ShowToast(Loc.T("图片存放位置已更新", "Screenshot folder updated"), false);
        }

        private void ShowToast(string message, bool error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, bool>(ShowToast), message, error);
                return;
            }
            PositionToast();
            _toast.ShowMessage(message, error);
        }

        private void PositionToast()
        {
            if (_content == null || _toast == null)
            {
                return;
            }
            int width = Math.Min((int)(380 * _dpi), Math.Max(220, _content.ClientSize.Width - 40));
            int height = (int)(42 * _dpi);
            int x = Math.Max(12, (_content.ClientSize.Width - width) / 2);
            int y = Math.Max(12, _content.ClientSize.Height - height - (int)(26 * _dpi));
            _toast.Bounds = new Rectangle(x, y, width, height);
        }

        public void AllowExitAndClose()
        {
            _allowClose = true;
            Close();
        }

        public void RequestExit()
        {
            EventHandler handler = ExitRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionToast();
            if (WindowState == FormWindowState.Minimized && !_hidePending)
            {
                BeginInvoke(new Action(delegate
                {
                    if (WindowState == FormWindowState.Minimized)
                    {
                        HideToTray(true);
                    }
                }));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose &&
                e.CloseReason != CloseReason.WindowsShutDown &&
                e.CloseReason != CloseReason.TaskManagerClosing)
            {
                if (SettingsStore.GetCloseBehavior() == CloseBehavior.Exit)
                {
                    base.OnFormClosing(e);
                    return;
                }
                e.Cancel = true;
                HideToTray(true);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (_currentView == AppView.Gallery && _gallery.HandleEscape())
                {
                    e.Handled = true;
                    return;
                }
                if (_currentView == AppView.Recycle && _recyclePage.Grid.HandleEscape())
                {
                    e.Handled = true;
                    return;
                }
                if (_currentView != AppView.Gallery)
                {
                    ShowView(AppView.Gallery);
                }
                else
                {
                    HideToTray(true);
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (_currentView == AppView.Gallery && _gallery.HandleDeleteSelection())
                {
                    e.Handled = true;
                }
                else if (_currentView == AppView.Recycle && _recyclePage.Grid.HandleDeleteSelection())
                {
                    e.Handled = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                if (_currentView == AppView.Gallery && _gallery.HandleSelectAll())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (_currentView == AppView.Recycle && _recyclePage.Grid.HandleSelectAll())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void GalleryShotActivated(ShotFile shot)
        {
            if (shot == null)
            {
                return;
            }
            List<ShotFile> shots = CaptureStore.LoadAll();
            int index = shots.FindIndex(delegate(ShotFile item)
            {
                return string.Equals(item.Path, shot.Path, StringComparison.OrdinalIgnoreCase);
            });
            if (index < 0)
            {
                shots.Insert(0, shot);
                index = 0;
            }
            using (PreviewForm preview = new PreviewForm(
                shots, index, Screen.FromControl(this).Bounds, Icon, false))
            {
                preview.ShowDialog(this);
            }
            RefreshCollectionsAfterPreview();
        }

        private void RecycleShotActivated(ShotFile shot)
        {
            if (shot == null)
            {
                return;
            }
            List<ShotFile> shots = CaptureStore.LoadRecycled();
            int index = shots.FindIndex(delegate(ShotFile item)
            {
                return string.Equals(item.Path, shot.Path, StringComparison.OrdinalIgnoreCase);
            });
            if (index < 0)
            {
                shots.Insert(0, shot);
                index = 0;
            }
            using (PreviewForm preview = new PreviewForm(
                shots, index, Screen.FromControl(this).Bounds, Icon, true))
            {
                preview.ShowDialog(this);
            }
            RefreshCollectionsAfterPreview();
        }

        private void RefreshCollectionsAfterPreview()
        {
            _gallery.MarkDirty();
            if (_gallery.Visible)
            {
                _gallery.Reload(false);
            }
            _recyclePage.Grid.MarkDirty();
            if (_recyclePage.Visible)
            {
                _recyclePage.RefreshData();
            }
            UpdateRecycleCount();
        }
    }
}
