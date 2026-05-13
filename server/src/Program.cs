using System.Net;
using System.Net.Sockets;

namespace EasySaveServer
{
    // TCP log server. Each connected client sends UTF-8 log lines (newline-
    // terminated). The server appends every received line to a daily log file
    // under the configured output directory. Multiple clients and connections
    // are handled concurrently; writes are serialized through a lock so lines
    // from different machines never interleave inside the file.
    internal sealed class LogServer
    {
        private readonly int _port;
        private readonly string _outputDir;
        private readonly object _writeLock = new();

        public LogServer(int port, string outputDir)
        {
            _port = port;
            _outputDir = outputDir;
            Directory.CreateDirectory(outputDir);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            Console.WriteLine($"[EaseSave Log Server] listening on :{_port}, writing to {_outputDir}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync(ct); }
                    catch (OperationCanceledException) { break; }

                    _ = HandleClientAsync(client, ct);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                client.ReceiveTimeout = 30_000;
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

                try
                {
                    string? line;
                    while (!ct.IsCancellationRequested
                           && (line = await reader.ReadLineAsync(ct)) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        AppendLine(line);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { /* client disconnected */ }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[client error] {ex.Message}");
                }
            }
        }

        private void AppendLine(string line)
        {
            string fileName = $"{DateTime.Now:yyyy-MM-dd}.log";
            string path = Path.Combine(_outputDir, fileName);
            lock (_writeLock)
            {
                using var w = new StreamWriter(path, append: true, System.Text.Encoding.UTF8);
                w.WriteLine(line);
            }
        }
    }

    internal static class Program
    {
        static async Task Main(string[] args)
        {
            int port = 9000;
            string outputDir = "logs";

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--port" && int.TryParse(args[i + 1], out int p)) port = p;
                if (args[i] == "--output") outputDir = args[i + 1];
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            var server = new LogServer(port, outputDir);
            await server.RunAsync(cts.Token);

            Console.WriteLine("Server stopped.");
        }
    }
}
