using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace JiePing
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "JiePing";

        public static string ExecutablePath
        {
            get
            {
                try
                {
                    return Path.GetFullPath(Application.ExecutablePath);
                }
                catch
                {
                    return Application.ExecutablePath;
                }
            }
        }

        public static string CommandLine
        {
            get { return "\"" + ExecutablePath + "\" --startup"; }
        }

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    {
                        if (key == null)
                        {
                            return false;
                        }
                        object value = key.GetValue(ValueName);
                        return value != null && !string.IsNullOrEmpty(value.ToString());
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool TrySetEnabled(bool enabled, out string error)
        {
            error = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        error = Loc.T("无法打开当前用户的启动项。",
                            "Cannot open the current user's startup registry key.");
                        return false;
                    }
                    if (enabled)
                    {
                        key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex is UnauthorizedAccessException
                    ? Loc.T("权限不足，无法修改开机启动项。",
                        "Permission denied. Cannot change the startup setting.")
                    : ex.Message;
                CaptureStore.Log("startup toggle failed: " + ex.Message);
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            string error;
            TrySetEnabled(enabled, out error);
        }

        public static void RepairPathIfNeeded()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return;
                    }
                    object value = key.GetValue(ValueName);
                    if (value == null)
                    {
                        return;
                    }
                    if (!string.Equals(value.ToString(), CommandLine, StringComparison.OrdinalIgnoreCase))
                    {
                        using (RegistryKey writable = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                        {
                            if (writable != null)
                            {
                                writable.SetValue(ValueName, CommandLine, RegistryValueKind.String);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
