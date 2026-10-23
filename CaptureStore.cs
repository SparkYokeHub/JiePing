using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JiePing
{
    internal sealed class ShotFile
    {
        public string Path;
        public DateTime Time;
        public string Name;

        public ShotFile(string path, DateTime time)
        {
            Path = path;
            Time = time;
            Name = System.IO.Path.GetFileName(path);
        }
    }

    internal sealed class ThumbResult
    {
        public Bitmap Image;
        public int Width;
        public int Height;
        public bool Failed;
    }

    internal static class CaptureStore
    {
        private const string ShotPattern = "JiePing_*.png";
        private static readonly object SaveLock = new object();

        private sealed class MovePair
        {
            public string Source;
            public string Destination;
        }

        public static string HomeDir
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("JIEPING_HOME");
                if (!string.IsNullOrEmpty(overridden))
                {
                    return overridden;
                }
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(local))
                {
                    local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                }
                if (string.IsNullOrEmpty(local))
                {
                    local = Path.GetTempPath();
                }
                return Path.Combine(local, "JiePing");
            }
        }

        public static string ShotsDir
        {
            get { return SettingsStore.ImageFolder; }
        }

        public static string RecycleDir
        {
            get { return Path.Combine(ShotsDir, ".recycle"); }
        }

        public static string LogDir
        {
            get { return Path.Combine(HomeDir, "logs"); }
        }

        public static void EnsureDirectories()
        {
            try
            {
                Directory.CreateDirectory(ShotsDir);
                Directory.CreateDirectory(RecycleDir);
                HideDirectory(RecycleDir);
            }
            catch
            {
            }
            try
            {
                Directory.CreateDirectory(LogDir);
            }
            catch
            {
            }
        }

        private static void HideDirectory(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Hidden) == 0)
                {
                    File.SetAttributes(path, attributes | FileAttributes.Hidden);
                }
            }
            catch
            {
            }
        }

        public static bool IsStorageWritable()
        {
            try
            {
                EnsureDirectories();
                string test = Path.Combine(ShotsDir, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);
                string testRecycle = Path.Combine(RecycleDir, ".write-test");
                File.WriteAllText(testRecycle, "ok");
                File.Delete(testRecycle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string Save(Bitmap image)
        {
            EnsureDirectories();
            lock (SaveLock)
            {
                DateTime now = DateTime.Now;
                string path = string.Empty;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    string name = string.Format(
                        "JiePing_{0:yyyyMMdd_HHmmss_fff}{1}.png",
                        now,
                        attempt == 0 ? string.Empty : "_" + attempt);
                    path = Path.Combine(ShotsDir, name);
                    if (!File.Exists(path))
                    {
                        break;
                    }
                }

                image.Save(path, ImageFormat.Png);
                return path;
            }
        }

        public static List<ShotFile> LoadAll()
        {
            return LoadFrom(ShotsDir, ShotPattern);
        }

        public static List<ShotFile> LoadRecycled()
        {
            return LoadFrom(RecycleDir, ShotPattern);
        }

        private static List<ShotFile> LoadFrom(string directory, string pattern)
        {
            EnsureDirectories();
            List<ShotFile> items = new List<ShotFile>();
            try
            {
                string[] files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        FileInfo info = new FileInfo(files[i]);
                        items.Add(new ShotFile(files[i], info.LastWriteTime));
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            items.Sort(delegate(ShotFile a, ShotFile b)
            {
                int byTime = b.Time.CompareTo(a.Time);
                if (byTime != 0)
                {
                    return byTime;
                }
                return string.CompareOrdinal(b.Name, a.Name);
            });
            return items;
        }

        public static int CountRecycled()
        {
            try
            {
                EnsureDirectories();
                return Directory.GetFiles(RecycleDir, ShotPattern, SearchOption.TopDirectoryOnly).Length;
            }
            catch
            {
                return 0;
            }
        }

        public static string MoveToRecycle(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            try
            {
                EnsureDirectories();
                string destination = UniqueDestination(RecycleDir, Path.GetFileName(path), null);
                File.Move(path, destination);
                return destination;
            }
            catch (Exception ex)
            {
                Log("move to recycle failed: " + ex.Message);
                return null;
            }
        }

        public static string RestoreFromRecycle(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            try
            {
                EnsureDirectories();
                string destination = UniqueDestination(ShotsDir, Path.GetFileName(path), null);
                File.Move(path, destination);
                return destination;
            }
            catch (Exception ex)
            {
                Log("restore from recycle failed: " + ex.Message);
                return null;
            }
        }

        public static bool DeletePermanently(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return !File.Exists(path);
            }
            catch (Exception ex)
            {
                Log("permanent delete failed: " + ex.Message);
                return false;
            }
        }

        public static bool ChangeImageFolder(string folder, out string error)
        {
            error = null;
            string target;
            try
            {
                target = Path.GetFullPath(folder.Trim());
            }
            catch (Exception ex)
            {
                error = Loc.T("路径无效：", "Invalid path: ") + ex.Message;
                return false;
            }

            string oldShots = ShotsDir;
            string oldRecycle = RecycleDir;
            string newRecycle = Path.Combine(target, ".recycle");

            if (string.Equals(target, oldShots, StringComparison.OrdinalIgnoreCase))
            {
                return SettingsStore.SetImageFolder(target, out error);
            }
            if (string.Equals(target, oldRecycle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newRecycle, oldShots, StringComparison.OrdinalIgnoreCase) ||
                IsInside(target, oldRecycle))
            {
                error = Loc.T("这个文件夹与回收站目录冲突，请选择其他位置。",
                    "This folder conflicts with the recycle bin folder. Choose another.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(target);
                Directory.CreateDirectory(newRecycle);
                HideDirectory(newRecycle);
                string test = Path.Combine(target, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);
            }
            catch (Exception ex)
            {
                error = Loc.T("无法写入该文件夹：", "Cannot write to this folder: ") + ex.Message;
                return false;
            }

            List<MovePair> moves = new List<MovePair>();
            HashSet<string> reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectMoves(oldShots, target, moves, reserved);
            CollectMoves(oldRecycle, newRecycle, moves, reserved);

            List<MovePair> completed = new List<MovePair>();
            try
            {
                for (int i = 0; i < moves.Count; i++)
                {
                    MovePair pair = moves[i];
                    File.Move(pair.Source, pair.Destination);
                    completed.Add(pair);
                }
            }
            catch (Exception ex)
            {
                Rollback(completed);
                error = Loc.T("迁移截图失败：", "Failed to move screenshots: ") + ex.Message;
                Log("change folder move failed: " + ex.Message);
                return false;
            }

            string settingError;
            if (!SettingsStore.SetImageFolder(target, out settingError))
            {
                Rollback(completed);
                error = Loc.T("保存设置失败：", "Failed to save settings: ") +
                    (settingError ?? Loc.T("未知错误", "unknown error"));
                return false;
            }

            try
            {
                if (Directory.Exists(oldRecycle) &&
                    !string.Equals(oldRecycle, newRecycle, StringComparison.OrdinalIgnoreCase) &&
                    Directory.GetFileSystemEntries(oldRecycle).Length == 0)
                {
                    Directory.Delete(oldRecycle);
                }
            }
            catch
            {
            }
            return true;
        }

        private static bool IsInside(string path, string parent)
        {
            try
            {
                string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string q = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return p.StartsWith(q, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void CollectMoves(string sourceDir, string targetDir, List<MovePair> moves, HashSet<string> reserved)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                {
                    return;
                }
                string[] files = Directory.GetFiles(sourceDir, ShotPattern, SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    string destination = UniqueDestination(targetDir, Path.GetFileName(files[i]), reserved);
                    if (string.Equals(files[i], destination, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    MovePair pair = new MovePair();
                    pair.Source = files[i];
                    pair.Destination = destination;
                    moves.Add(pair);
                }
            }
            catch (Exception ex)
            {
                Log("collect moves failed: " + ex.Message);
            }
        }

        private static void Rollback(List<MovePair> completed)
        {
            for (int i = completed.Count - 1; i >= 0; i--)
            {
                try
                {
                    MovePair pair = completed[i];
                    if (File.Exists(pair.Destination) && !File.Exists(pair.Source))
                    {
                        File.Move(pair.Destination, pair.Source);
                    }
                }
                catch
                {
                }
            }
        }

        private static string UniqueDestination(string directory, string fileName, HashSet<string> reserved)
        {
            Directory.CreateDirectory(directory);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string candidate = Path.Combine(directory, fileName);
            int counter = 1;
            while (File.Exists(candidate) || (reserved != null && reserved.Contains(candidate)))
            {
                candidate = Path.Combine(directory, baseName + "_" + counter + extension);
                counter++;
            }
            if (reserved != null)
            {
                reserved.Add(candidate);
            }
            return candidate;
        }

        public static ThumbResult LoadThumbnail(string path, int maxWidth, int maxHeight)
        {
            ThumbResult result = new ThumbResult();
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Image source = Image.FromStream(fs, false, false))
                {
                    result.Width = source.Width;
                    result.Height = source.Height;
                    double scale = Math.Min(
                        (double)maxWidth / Math.Max(1, source.Width),
                        (double)maxHeight / Math.Max(1, source.Height));
                    scale = Math.Min(1.0, scale);
                    int w = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int h = Math.Max(1, (int)Math.Round(source.Height * scale));

                    Bitmap thumb = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    thumb.SetResolution(96f, 96f);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetWrapMode(WrapMode.TileFlipXY);
                            g.DrawImage(source, new Rectangle(0, 0, w, h), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    result.Image = thumb;
                }
            }
            catch
            {
                result.Failed = true;
            }
            return result;
        }

        public static Bitmap LoadFullImage(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (Image source = Image.FromStream(fs, false, false))
            {
                Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
                copy.SetResolution(96f, 96f);
                using (Graphics g = Graphics.FromImage(copy))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }
                return copy;
            }
        }

        public static bool CopyToClipboard(Bitmap image)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    Clipboard.SetImage(image);
                    return true;
                }
                catch
                {
                    Thread.Sleep(40);
                }
            }
            return false;
        }

        public static void RevealInExplorer(string path)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch
            {
            }
        }

        public static void OpenFolder()
        {
            try
            {
                EnsureDirectories();
                System.Diagnostics.Process.Start("explorer.exe", "\"" + ShotsDir + "\"");
            }
            catch
            {
            }
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string path = Path.Combine(LogDir, "app.log");
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 512 * 1024)
                {
                    try { File.Delete(path); } catch { }
                }
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
