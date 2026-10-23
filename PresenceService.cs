using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace JiePing
{
    internal sealed class PresenceService : IDisposable
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread _thread;
        private string _clientId;

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }
            _clientId = LoadClientId();
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "JiePing presence";
            _thread.Start();
        }

        public void Stop()
        {
            _stop.Set();
            ThreadPool.QueueUserWorkItem(delegate { Send("api/offline"); });
        }

        private void Loop()
        {
            while (true)
            {
                Send("api/presence");
                if (_stop.WaitOne(30000))
                {
                    break;
                }
            }
        }

        private void Send(string path)
        {
            try
            {
                string body = "id=" + Uri.EscapeDataString(_clientId ?? string.Empty) +
                              "&version=" + Uri.EscapeDataString(UpdateService.CurrentVersion) +
                              "&time=" + Uri.EscapeDataString(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                byte[] data = Encoding.UTF8.GetBytes(body);
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UpdateService.Endpoint(path));
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;
                request.ContentLength = data.Length;
                request.UserAgent = "JiePing/" + UpdateService.CurrentVersion;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                }
            }
            catch
            {
                // Presence is intentionally silent: the client never shows or
                // prompts anything when the backend is offline.
            }
        }

        private static string LoadClientId()
        {
            try
            {
                string path = Path.Combine(CaptureStore.HomeDir, "presence-id.txt");
                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path).Trim();
                    if (existing.Length > 0)
                    {
                        return existing;
                    }
                }
                string id = Guid.NewGuid().ToString("N");
                File.WriteAllText(path, id, new UTF8Encoding(false));
                return id;
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        public void Dispose()
        {
            Stop();
            _stop.Dispose();
        }
    }
}
