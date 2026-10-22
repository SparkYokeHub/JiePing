using System;
using System.IO;
using System.Xml;

namespace JiePing
{
    internal enum CloseBehavior
    {
        Tray,
        Exit
    }

    internal static class SettingsStore
    {
        private static readonly object Sync = new object();
        private static string _cachedImageFolder;

        public static string SettingsFile
        {
            get { return Path.Combine(CaptureStore.HomeDir, "settings.xml"); }
        }

        public static string DefaultImageFolder
        {
            get { return Path.Combine(CaptureStore.HomeDir, "Screenshots"); }
        }

        public static string ImageFolder
        {
            get
            {
                lock (Sync)
                {
                    if (!string.IsNullOrEmpty(_cachedImageFolder))
                    {
                        return _cachedImageFolder;
                    }
                    string folder = null;
                    try
                    {
                        XmlDocument doc = LoadDocument();
                        XmlNode node = doc.SelectSingleNode("/settings/imageFolder");
                        if (node != null)
                        {
                            string value = (node.InnerText ?? string.Empty).Trim();
                            if (!string.IsNullOrEmpty(value))
                            {
                                folder = Path.GetFullPath(value);
                            }
                        }
                    }
                    catch
                    {
                        folder = null;
                    }

                    if (string.IsNullOrEmpty(folder))
                    {
                        folder = Path.GetFullPath(DefaultImageFolder);
                    }
                    _cachedImageFolder = folder;
                    return folder;
                }
            }
        }

        public static HotkeyConfig GetHotkey()
        {
            lock (Sync)
            {
                HotkeyConfig config = HotkeyConfig.CreateDefault();
                try
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement node = doc.SelectSingleNode("/settings/hotkey") as XmlElement;
                    if (node == null)
                    {
                        return config;
                    }

                    config.Ctrl = IsTrue(node.GetAttribute("ctrl"));
                    config.Alt = IsTrue(node.GetAttribute("alt"));
                    config.Shift = IsTrue(node.GetAttribute("shift"));
                    config.Win = IsTrue(node.GetAttribute("win"));

                    int key;
                    if (int.TryParse(node.GetAttribute("key"), out key) && key > 0 && !HotkeyNames.IsModifier(key))
                    {
                        config.KeyCode = key;
                    }
                    else
                    {
                        config.KeyCode = 0;
                    }

                    config.Mode = string.Equals(node.GetAttribute("mode"), "hold",
                        StringComparison.OrdinalIgnoreCase)
                        ? HotkeyTriggerMode.Hold
                        : HotkeyTriggerMode.Tap;

                    int min;
                    int max;
                    int.TryParse(node.GetAttribute("minDelay"), out min);
                    int.TryParse(node.GetAttribute("maxDelay"), out max);
                    min = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, min));
                    max = Math.Max(0, Math.Min(HotkeyConfig.MaxDelayMs, max));
                    if (max < min)
                    {
                        int swap = min;
                        min = max;
                        max = swap;
                    }
                    config.MinDelayMs = min;
                    config.MaxDelayMsValue = max;

                    if (!config.IsValid)
                    {
                        return HotkeyConfig.CreateDefault();
                    }
                    return config;
                }
                catch
                {
                    return config;
                }
            }
        }

        public static string GetUpdateUrl()
        {
            lock (Sync)
            {
                try
                {
                    XmlDocument doc = LoadDocument();
                    XmlNode node = doc.SelectSingleNode("/settings/updateUrl");
                    if (node != null)
                    {
                        string value = (node.InnerText ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(value) && LooksLikeManifestUrl(value))
                        {
                            return value;
                        }
                    }
                }
                catch
                {
                }
                return UpdateService.DefaultManifestUrl;
            }
        }

        public static string GetServiceUrl()
        {
            lock (Sync)
            {
                try
                {
                    XmlDocument doc = LoadDocument();
                    XmlNode service = doc.SelectSingleNode("/settings/serviceUrl");
                    if (service != null)
                    {
                        string value = (service.InnerText ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value.TrimEnd('/');
                        }
                    }
                    XmlNode legacy = doc.SelectSingleNode("/settings/updateUrl");
                    if (legacy != null)
                    {
                        string value = (legacy.InnerText ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(value) && !LooksLikeManifestUrl(value))
                        {
                            return value.TrimEnd('/');
                        }
                    }
                }
                catch
                {
                }
                return UpdateService.DefaultBaseUrl;
            }
        }

        private static bool LooksLikeManifestUrl(string value)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            return lower.EndsWith(".txt") ||
                   lower.Contains("/releases/") ||
                   lower.Contains("update.txt");
        }

        public static bool SaveUpdateUrl(string url, out string error)
        {
            error = null;
            try
            {
                lock (Sync)
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement root = doc.DocumentElement;
                    if (root == null)
                    {
                        root = doc.CreateElement("settings");
                        doc.AppendChild(root);
                    }
                    XmlElement node = root.SelectSingleNode("updateUrl") as XmlElement;
                    if (node == null)
                    {
                        node = doc.CreateElement("updateUrl");
                        root.AppendChild(node);
                    }
                    node.InnerText = (url ?? string.Empty).Trim().TrimEnd('/');
                    SaveDocument(doc);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CaptureStore.Log("update url save failed: " + ex.Message);
                return false;
            }
        }

        public static AppLanguage GetLanguage()
        {
            lock (Sync)
            {
                try
                {
                    XmlDocument doc = LoadDocument();
                    XmlNode node = doc.SelectSingleNode("/settings/language");
                    if (node != null && string.Equals((node.InnerText ?? string.Empty).Trim(),
                        "en", StringComparison.OrdinalIgnoreCase))
                    {
                        return AppLanguage.English;
                    }
                }
                catch
                {
                }
                return AppLanguage.Chinese;
            }
        }

        public static bool SaveLanguage(AppLanguage language, out string error)
        {
            error = null;
            try
            {
                lock (Sync)
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement root = doc.DocumentElement;
                    if (root == null)
                    {
                        root = doc.CreateElement("settings");
                        doc.AppendChild(root);
                    }
                    XmlElement node = root.SelectSingleNode("language") as XmlElement;
                    if (node == null)
                    {
                        node = doc.CreateElement("language");
                        root.AppendChild(node);
                    }
                    node.InnerText = language == AppLanguage.English ? "en" : "zh";
                    SaveDocument(doc);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CaptureStore.Log("language save failed: " + ex.Message);
                return false;
            }
        }

        public static CloseBehavior GetCloseBehavior()
        {
            lock (Sync)
            {
                try
                {
                    XmlDocument doc = LoadDocument();
                    XmlNode node = doc.SelectSingleNode("/settings/closeBehavior");
                    if (node != null && string.Equals((node.InnerText ?? string.Empty).Trim(),
                        "exit", StringComparison.OrdinalIgnoreCase))
                    {
                        return CloseBehavior.Exit;
                    }
                }
                catch
                {
                }
                return CloseBehavior.Tray;
            }
        }

        public static bool SaveCloseBehavior(CloseBehavior behavior, out string error)
        {
            error = null;
            try
            {
                lock (Sync)
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement root = doc.DocumentElement;
                    if (root == null)
                    {
                        root = doc.CreateElement("settings");
                        doc.AppendChild(root);
                    }
                    XmlElement node = root.SelectSingleNode("closeBehavior") as XmlElement;
                    if (node == null)
                    {
                        node = doc.CreateElement("closeBehavior");
                        root.AppendChild(node);
                    }
                    node.InnerText = behavior == CloseBehavior.Exit ? "exit" : "tray";
                    SaveDocument(doc);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CaptureStore.Log("close behavior save failed: " + ex.Message);
                return false;
            }
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        public static bool SaveHotkey(HotkeyConfig config, out string error)
        {
            error = null;
            if (config == null || !config.IsValid)
            {
                error = Loc.T("快捷键组合无效。", "Invalid shortcut combination.");
                return false;
            }
            try
            {
                lock (Sync)
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement root = doc.DocumentElement;
                    if (root == null)
                    {
                        root = doc.CreateElement("settings");
                        doc.AppendChild(root);
                    }
                    XmlElement node = root.SelectSingleNode("hotkey") as XmlElement;
                    if (node == null)
                    {
                        node = doc.CreateElement("hotkey");
                        root.AppendChild(node);
                    }
                    node.SetAttribute("ctrl", config.Ctrl ? "true" : "false");
                    node.SetAttribute("alt", config.Alt ? "true" : "false");
                    node.SetAttribute("shift", config.Shift ? "true" : "false");
                    node.SetAttribute("win", config.Win ? "true" : "false");
                    node.SetAttribute("key", config.KeyCode.ToString());
                    node.SetAttribute("mode", config.Mode == HotkeyTriggerMode.Hold ? "hold" : "tap");
                    node.SetAttribute("minDelay", config.MinDelayMs.ToString());
                    node.SetAttribute("maxDelay", config.MaxDelayMsValue.ToString());
                    SaveDocument(doc);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CaptureStore.Log("hotkey save failed: " + ex.Message);
                return false;
            }
        }

        public static bool SetImageFolder(string folder, out string error)
        {
            error = null;
            try
            {
                string full = Path.GetFullPath(folder.Trim());
                Directory.CreateDirectory(full);
                lock (Sync)
                {
                    XmlDocument doc = LoadDocument();
                    XmlElement root = doc.DocumentElement;
                    if (root == null)
                    {
                        root = doc.CreateElement("settings");
                        doc.AppendChild(root);
                    }
                    XmlElement item = root.SelectSingleNode("imageFolder") as XmlElement;
                    if (item == null)
                    {
                        item = doc.CreateElement("imageFolder");
                        root.AppendChild(item);
                    }
                    item.InnerText = full;
                    SaveDocument(doc);
                    _cachedImageFolder = full;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CaptureStore.Log("settings write failed: " + ex.Message);
                return false;
            }
        }

        private static XmlDocument LoadDocument()
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                if (File.Exists(SettingsFile))
                {
                    doc.Load(SettingsFile);
                }
            }
            catch
            {
                doc = new XmlDocument();
            }
            if (doc.DocumentElement == null)
            {
                XmlElement root = doc.CreateElement("settings");
                doc.AppendChild(root);
            }
            return doc;
        }

        private static void SaveDocument(XmlDocument doc)
        {
            Directory.CreateDirectory(CaptureStore.HomeDir);
            string temp = SettingsFile + ".tmp";
            doc.Save(temp);
            if (File.Exists(SettingsFile))
            {
                File.Copy(temp, SettingsFile, true);
                File.Delete(temp);
            }
            else
            {
                File.Move(temp, SettingsFile);
            }
        }
    }
}
