using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace JiePing
{
    internal static class CommandChannel
    {
        public const string PipeName = "JiePing.Command.v2";

        public static bool TrySend(string command)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                        ".", PipeName, PipeDirection.Out, PipeOptions.None))
                    {
                        pipe.Connect(250);
                        using (StreamWriter writer = new StreamWriter(pipe, new UTF8Encoding(false)))
                        {
                            writer.WriteLine(command ?? "show");
                            writer.Flush();
                        }
                    }
                    return true;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
            return false;
        }
    }
}
