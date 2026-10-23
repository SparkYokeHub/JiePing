using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class UpdateManifest
    {
        public string Version;
        public string Url;
        public string Sha256;
        public long Size;
        public string Notes;
        public string Published;
    }

    internal static class UpdateService
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:8787";
        public const string DefaultManifestUrl =
            "https://github.com/SparkYokeHub/JiePing/releases/latest/download/update.txt";
        private static string _baseUrl = DefaultBaseUrl;
        private static string _manifestUrl = DefaultManifestUrl;

        public static string BaseUrl
        {
            get { return _baseUrl; }
        }

        public static string ManifestUrl
        {
            get { return _manifestUrl; }
        }

        public static string CurrentVersion
        {
            get { return Application.ProductVersion; }
        }

        public static void SetBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _baseUrl = DefaultBaseUrl;
                return;
            }
            _baseUrl = url.Trim().TrimEnd('/');
        }

        public static void SetManifestUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _manifestUrl = DefaultManifestUrl;
                return;
            }
            _manifestUrl = url.Trim();
        }

        public static string Endpoint(string path)
        {
            return _baseUrl + "/" + (path ?? string.Empty).TrimStart('/');
        }

        public static UpdateManifest Check()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_manifestUrl);
            request.Method = "GET";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.UserAgent = "JiePing/" + CurrentVersion;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                return ParseManifest(text);
            }
        }

        private static UpdateManifest ParseManifest(string text)
        {
            UpdateManifest manifest = new UpdateManifest();
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, index).Trim().ToLowerInvariant();
                string value = line.Substring(index + 1).Trim();
                if (key == "version") manifest.Version = value;
                else if (key == "url") manifest.Url = value;
                else if (key == "sha256") manifest.Sha256 = value;
                else if (key == "size") long.TryParse(value, out manifest.Size);
                else if (key == "notes") manifest.Notes = value.Replace("\\n", "\n");
                else if (key == "published") manifest.Published = value;
            }
            if (string.IsNullOrEmpty(manifest.Version))
            {
                throw new InvalidOperationException(Loc.T("更新信息缺少版本号。",
                    "The update response has no version."));
            }
            if (string.IsNullOrEmpty(manifest.Url))
            {
                throw new InvalidOperationException(Loc.T("更新信息缺少下载地址。",
                    "The update response has no download URL."));
            }
            Uri absolute;
            if (Uri.TryCreate(manifest.Url, UriKind.Absolute, out absolute))
            {
                manifest.Url = absolute.ToString();
            }
            else
            {
                manifest.Url = new Uri(new Uri(_manifestUrl), manifest.Url).ToString();
            }
            return manifest;
        }

        public static bool IsNewer(string remote)
        {
            Version localVersion;
            Version remoteVersion;
            if (!Version.TryParse(CurrentVersion, out localVersion) ||
                !Version.TryParse(remote, out remoteVersion))
            {
                return !string.Equals(remote, CurrentVersion, StringComparison.OrdinalIgnoreCase);
            }
            return remoteVersion > localVersion;
        }

        public static string Download(UpdateManifest manifest, Action<int> progress)
        {
            string folder = Path.Combine(CaptureStore.HomeDir, "updates");
            Directory.CreateDirectory(folder);
            string fileName = "JiePing-" + SafeVersion(manifest.Version) + ".exe";
            string destination = Path.Combine(folder, fileName);
            string temp = destination + ".part";

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(manifest.Url);
            request.Method = "GET";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "JiePing/" + CurrentVersion;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (FileStream file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = response.ContentLength > 0 ? response.ContentLength : manifest.Size;
                byte[] buffer = new byte[81920];
                long received = 0;
                int lastPercent = -1;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    file.Write(buffer, 0, read);
                    received += read;
                    if (progress != null && total > 0)
                    {
                        int percent = (int)Math.Min(100, received * 100 / total);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress(percent);
                        }
                    }
                }
                file.Flush();
            }

            if (!string.IsNullOrEmpty(manifest.Sha256))
            {
                string actual = ComputeSha256(temp);
                if (!string.Equals(actual, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(temp); } catch { }
                    throw new InvalidOperationException(Loc.T("安装包校验失败，文件可能不完整。",
                        "The package hash does not match. The download may be incomplete."));
                }
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            File.Move(temp, destination);
            return destination;
        }

        private static string SafeVersion(string version)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < (version ?? string.Empty).Length; i++)
            {
                char c = version[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_');
            }
            return builder.Length == 0 ? "latest" : builder.ToString();
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream file = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(file);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        public static string SendFeedback(string message, string contact)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Loc.T("请输入反馈内容。", "Please enter your feedback.");
            }
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string body = "message=" + Uri.EscapeDataString(message) +
                          "&version=" + Uri.EscapeDataString(CurrentVersion) +
                          "&contact=" + Uri.EscapeDataString(contact ?? string.Empty) +
                          "&time=" + Uri.EscapeDataString(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            byte[] data = Encoding.UTF8.GetBytes(body);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Endpoint("api/feedback"));
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            request.ContentLength = data.Length;
            request.UserAgent = "JiePing/" + CurrentVersion;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return Loc.T("发送失败，请稍后再试。", "Send failed. Please try again.");
                }
            }
            return null;
        }

        public static string StartSelfUpdate(string downloadedFile, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(downloadedFile))
                {
                    error = Loc.T("下载的安装包不存在。", "The downloaded package does not exist.");
                    return null;
                }
                string current = Application.ExecutablePath;
                string scriptDir = Path.Combine(CaptureStore.HomeDir, "updates");
                Directory.CreateDirectory(scriptDir);
                string script = Path.Combine(scriptDir,
                    "JiePingUpdate-" + Guid.NewGuid().ToString("N") + ".cmd");
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("@echo off");
                builder.AppendLine("setlocal");
                builder.AppendLine("ping 127.0.0.1 -n 4 >nul");
                builder.AppendLine("copy /Y \"" + downloadedFile + "\" \"" + current + "\" >nul");
                builder.AppendLine("if errorlevel 1 goto startold");
                builder.AppendLine("start \"\" \"" + current + "\"");
                builder.AppendLine("del \"%~f0\"");
                builder.AppendLine("exit /b");
                builder.AppendLine(":startold");
                builder.AppendLine("start \"\" \"" + current + "\"");
                builder.AppendLine("del \"%~f0\"");
                File.WriteAllText(script, builder.ToString(), Encoding.Default);

                ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info);
                return script;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }
}
