using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class JiePingContext : ApplicationContext
    {
        internal const int CommandShowGallery = 1;
        internal const int CommandCapture = 2;
        internal const int CommandExit = 3;

        private readonly MainForm _main;
        private readonly NotifyIcon _tray;
        private readonly HotkeyManager _hotkey;
        private readonly Icon _icon;
        private readonly bool _ownsIcon;
        private ToolStripMenuItem _captureMenuItem;
        private ContextMenuStrip _trayMenu;
        private CaptureOverlay _overlay;
        private volatile bool _capturing;
        private volatile bool _exiting;
        private NamedPipeServerStream _commandPipe;
        private PresenceService _presence;
        private bool _shownTrayHint;
        private bool _notifiedFirstShot;

        public JiePingContext(bool startedHidden)
        {
            CaptureStore.EnsureDirectories();
            StartupManager.RepairPathIfNeeded();
            Loc.IsEnglish = SettingsStore.GetLanguage() == AppLanguage.English;
            UpdateService.SetBaseUrl(SettingsStore.GetUpdateUrl());
            _presence = new PresenceService();
            _presence.Start();

            _icon = LoadAppIcon(out _ownsIcon);
            _main = new MainForm(_icon);
            _main.HiddenToTray += MainHiddenToTray;
            _main.LanguageChanged += delegate { ApplyLanguage(); };
            _main.ExitRequested += delegate { ExitApp(); };
            _main.FormClosed += MainFormClosed;
            _main.SetCommandMessage(Native.RegisterWindowMessage("JiePing.Command.v1"));
            _main.CommandReceived += MainCommandReceived;
            StartCommandServer();
            if (_main.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Unable to create the gallery window handle.");
            }

            _tray = new NotifyIcon();
            _tray.Icon = _icon;
            _tray.Text = Loc.T("截屏", "JiePing");
            _tray.Visible = true;
            _trayMenu = BuildTrayMenu();
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { ShowGallery(); };
            _tray.BalloonTipClicked += delegate { ShowGallery(); };

            _hotkey = new HotkeyManager(StartCapture);
            _main.HotkeyChanged += delegate
            {
                if (!_exiting)
                {
                    _hotkey.ReloadConfig();
                    UpdateTrayHotkey();
                }
            };
            Application.Idle += OnApplicationIdle;
            UpdateTrayHotkey();

            if (!CaptureStore.IsStorageWritable())
            {
                ShowTrayBalloon(
                    Loc.T("截图目录不可写", "Screenshot folder is not writable"),
                    Loc.T("无法写入本机截图目录，请检查用户目录权限。",
                        "Cannot write to the screenshot folder. Check folder permissions."),
                    ToolTipIcon.Warning);
            }

            if (!startedHidden)
            {
                _main.ShowGallery();
            }
        }

        private void OnApplicationIdle(object sender, EventArgs e)
        {
            // A WH_KEYBOARD_LL hook installed before the WinForms message loop
            // exists can miss injected and real key events.  Installing it on
            // the first idle callback makes it reliable.
            Application.Idle -= OnApplicationIdle;
            if (!_exiting)
            {
                _hotkey.Install();
            }
        }

        private static Icon LoadAppIcon(out bool ownsIcon)
        {
            ownsIcon = false;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (stream != null)
                    {
                        Icon icon = new Icon(stream);
                        ownsIcon = true;
                        return icon;
                    }
                }
            }
            catch
            {
            }
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    ownsIcon = true;
                    return extracted;
                }
            }
            catch
            {
            }
            return SystemIcons.Application;
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            ContextMenuStrip menu = UiTheme.CreateMenu();
            menu.ShowCheckMargin = true;
            menu.ShowImageMargin = false;

            ToolStripMenuItem open = UiTheme.MenuItem(Loc.T("打开图库", "Open Library"));
            open.Font = UiTheme.UiFont(9.5f, FontStyle.Bold);
            open.Click += delegate { ShowGallery(); };

            ToolStripMenuItem capture = UiTheme.MenuItem(Loc.T("截图", "Capture"));
            HotkeyConfig hotkey = SettingsStore.GetHotkey();
            capture.ShortcutKeyDisplayString = hotkey.DisplayText;
            capture.Click += delegate { StartCapture(); };
            _captureMenuItem = capture;

            ToolStripMenuItem startup = UiTheme.MenuItem(Loc.T("开机自启动", "Start with Windows"));
            startup.CheckOnClick = false;
            startup.Checked = StartupManager.IsEnabled;
            startup.Click += delegate
            {
                StartupManager.SetEnabled(!StartupManager.IsEnabled);
                startup.Checked = StartupManager.IsEnabled;
            };

            ToolStripMenuItem folder = UiTheme.MenuItem(Loc.T("打开截图文件夹", "Open Screenshot Folder"));
            folder.Click += delegate { CaptureStore.OpenFolder(); };

            ToolStripMenuItem exit = UiTheme.MenuItem(Loc.T("退出截屏", "Exit JiePing"));
            exit.Click += delegate { ExitApp(); };

            menu.Items.Add(open);
            menu.Items.Add(capture);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startup);
            menu.Items.Add(folder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            menu.Opening += delegate { startup.Checked = StartupManager.IsEnabled; };
            return menu;
        }

        private void MainCommandReceived(int command)
        {
            if (command == CommandShowGallery)
            {
                ShowGallery();
            }
            else if (command == CommandCapture)
            {
                StartCapture();
            }
            else if (command == CommandExit)
            {
                ExitApp();
            }
        }

        private void StartCommandServer()
        {
            Thread thread = new Thread(CommandServerLoop);
            thread.IsBackground = true;
            thread.Name = "JiePing command pipe";
            thread.Start();
        }

        private void CommandServerLoop()
        {
            while (!_exiting)
            {
                try
                {
                    using (NamedPipeServerStream pipe = new NamedPipeServerStream(
                        CommandChannel.PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.None))
                    {
                        _commandPipe = pipe;
                        pipe.WaitForConnection();
                        using (StreamReader reader = new StreamReader(pipe, Encoding.UTF8))
                        {
                            string command = reader.ReadLine();
                            if (!string.IsNullOrEmpty(command))
                            {
                                DispatchExternalCommand(command.Trim());
                            }
                        }
                    }
                }
                catch
                {
                    if (_exiting)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }
                finally
                {
                    _commandPipe = null;
                }
            }
        }

        private void DispatchExternalCommand(string command)
        {
            Action action = delegate
            {
                if (string.Equals(command, "capture", StringComparison.OrdinalIgnoreCase))
                {
                    StartCapture();
                }
                else if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    ExitApp();
                }
                else
                {
                    ShowGallery();
                }
            };
            try
            {
                if (_main != null && _main.InvokeRequired)
                {
                    _main.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
            }
        }

        private void ShowGallery()
        {
            if (_exiting)
            {
                return;
            }
            _main.ShowGallery();
        }

        private void StartCapture()
        {
            if (_exiting || _capturing)
            {
                return;
            }
            _capturing = true;
            try
            {
                Rectangle bounds;
                Bitmap screen = CaptureOverlay.CaptureVirtualScreen(out bounds);
                _overlay = new CaptureOverlay(screen, bounds, OnCaptureFinished);
                _overlay.Show();
                _overlay.Activate();
            }
            catch (Exception ex)
            {
                CaptureStore.Log("capture start failed: " + ex.Message);
                _capturing = false;
                ShowCaptureError();
            }
        }

        private void OnCaptureFinished(Bitmap bitmap, Rectangle rect)
        {
            _overlay = null;
            if (_exiting)
            {
                if (bitmap != null)
                {
                    bitmap.Dispose();
                }
                _capturing = false;
                return;
            }
            try
            {
                if (bitmap == null)
                {
                    return;
                }

                try
                {
                    CaptureStore.Save(bitmap);
                }
                catch (Exception ex)
                {
                    CaptureStore.Log("save failed: " + ex.Message);
                    ShowCaptureError();
                    return;
                }

                CaptureStore.CopyToClipboard(bitmap);
                bitmap.Dispose();
                bitmap = null;
                _main.RefreshGallery();

                if (!_notifiedFirstShot)
                {
                    _notifiedFirstShot = true;
                    ShowTrayBalloon(
                        Loc.T("截图已保存", "Screenshot saved"),
                        Loc.T("已复制到剪贴板，并放入按时间排列的图库。",
                            "Copied to the clipboard and added to your library."),
                        ToolTipIcon.None);
                }
            }
            finally
            {
                if (bitmap != null)
                {
                    bitmap.Dispose();
                }
                _capturing = false;
            }
        }

        private void MainHiddenToTray(object sender, EventArgs e)
        {
            if (_shownTrayHint)
            {
                return;
            }
            _shownTrayHint = true;
            ShowTrayBalloon(
                Loc.T("截屏仍在托盘运行", "JiePing is still running in the tray"),
                Loc.T("按 ", "Press ") + SettingsStore.GetHotkey().DisplayText +
                Loc.T(" 随时截图，双击托盘图标打开图库。",
                    " to capture anytime. Double-click the tray icon to open the library."),
                ToolTipIcon.None);
        }

        private void ShowCaptureError()
        {
            ShowTrayBalloon(
                Loc.T("截图失败", "Capture failed"),
                Loc.T("请稍后再试，或检查是否有其他程序独占屏幕。",
                    "Try again, or check whether another program is blocking the screen."),
                ToolTipIcon.Warning);
        }

        private void ApplyLanguage()
        {
            try
            {
                if (_trayMenu != null)
                {
                    ContextMenuStrip old = _trayMenu;
                    _trayMenu = BuildTrayMenu();
                    _tray.ContextMenuStrip = _trayMenu;
                    old.Dispose();
                }
                UpdateTrayHotkey();
            }
            catch
            {
            }
        }

        private void UpdateTrayHotkey()
        {
            try
            {
                HotkeyConfig config = SettingsStore.GetHotkey();
                if (_captureMenuItem != null)
                {
                    _captureMenuItem.ShortcutKeyDisplayString = config.DisplayText;
                }
                string text = Loc.T("截屏 · ", "JiePing · ") + config.DisplayText;
                if (text.Length > 63)
                {
                    text = text.Substring(0, 60) + "...";
                }
                _tray.Text = text;
            }
            catch
            {
            }
        }

        private void ShowTrayBalloon(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _tray.ShowBalloonTip(2600, title, text, icon);
            }
            catch
            {
            }
        }

        private void MainFormClosed(object sender, FormClosedEventArgs e)
        {
            if (_exiting)
            {
                return;
            }
            _exiting = true;
            Application.Idle -= OnApplicationIdle;
            try
            {
                if (_presence != null)
                {
                    _presence.Stop();
                }
            }
            catch
            {
            }
            try
            {
                _hotkey.Dispose();
            }
            catch
            {
            }
            try
            {
                if (_overlay != null)
                {
                    _overlay.Close();
                    _overlay = null;
                }
            }
            catch
            {
            }
            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch
            {
            }
            ExitThread();
        }

        private void ExitApp()
        {
            if (_exiting)
            {
                return;
            }
            _exiting = true;
            Application.Idle -= OnApplicationIdle;
            try
            {
                if (_presence != null)
                {
                    _presence.Stop();
                }
            }
            catch
            {
            }
            try
            {
                _hotkey.Dispose();
            }
            catch
            {
            }
            try
            {
                if (_overlay != null)
                {
                    _overlay.Close();
                    _overlay = null;
                }
            }
            catch
            {
            }
            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch
            {
            }
            try
            {
                _main.AllowExitAndClose();
            }
            catch
            {
            }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_exiting)
                {
                    _exiting = true;
                    Application.Idle -= OnApplicationIdle;
                    try { _hotkey.Dispose(); } catch { }
                    try
                    {
                        if (_overlay != null)
                        {
                            _overlay.Close();
                        }
                    }
                    catch
                    {
                    }
                    try
                    {
                        _tray.Visible = false;
                        _tray.Dispose();
                    }
                    catch
                    {
                    }
                    try { _main.Dispose(); } catch { }
                }
                if (_ownsIcon && _icon != null)
                {
                    _icon.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
