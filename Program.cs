using System;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal static class Program
    {
        private static Mutex _instanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            Native.SetProcessDPIAware();

            bool createdNew;
            _instanceMutex = new Mutex(true, @"Local\JiePing_Screenshot_1_0", out createdNew);
            bool startup = HasArg(args, "--startup");

            if (!createdNew)
            {
                if (!startup)
                {
                    string command = CommandStringFromArgs(args);
                    if (!CommandChannel.TrySend(command))
                    {
                        BroadcastCommand(CommandFromArgs(args));
                    }
                }
                return;
            }

            if (startup)
            {
                // Let logon finish its disk and shell work before this tiny tray
                // process initialises.  Normal launches remain instant.
                Thread.Sleep(2600);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                CaptureStore.Log("thread exception: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                CaptureStore.Log("unhandled exception: " + e.ExceptionObject);
            };

            CaptureStore.EnsureDirectories();
            try
            {
                using (JiePingContext context = new JiePingContext(startup))
                {
                    Application.Run(context);
                }
            }
            catch (Exception ex)
            {
                CaptureStore.Log("fatal: " + ex);
            }
            finally
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                }
                catch
                {
                }
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
        }

        private static bool HasArg(string[] args, string value)
        {
            if (args == null)
            {
                return false;
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static int CommandFromArgs(string[] args)
        {
            if (HasArg(args, "--capture"))
            {
                return JiePingContext.CommandCapture;
            }
            if (HasArg(args, "--exit"))
            {
                return JiePingContext.CommandExit;
            }
            return JiePingContext.CommandShowGallery;
        }

        private static string CommandStringFromArgs(string[] args)
        {
            if (HasArg(args, "--capture"))
            {
                return "capture";
            }
            if (HasArg(args, "--exit"))
            {
                return "exit";
            }
            return "show";
        }

        private static void BroadcastCommand(int command)
        {
            try
            {
                uint message = Native.RegisterWindowMessage("JiePing.Command.v1");
                if (message != 0)
                {
                    Native.PostMessage(Native.HWND_BROADCAST, message, new IntPtr(command), IntPtr.Zero);
                }
            }
            catch
            {
            }
        }
    }
}
