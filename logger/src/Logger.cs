using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace EasyLog
{
    public enum LogFormat { Text, JSON, XML }

    // Where log lines are sent. Remote = TCP server only. Both = file + TCP.
    public enum LogMode { Local, Remote, Both }

    public record LogInfo
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required string Action { get; init; }
        public required long FileSize { get; init; }        // bytes
        public required int TransferTime { get; init; }     // milliseconds
        public required int EncryptionTime { get; init; }     // milliseconds

        private string TextFormat() {
            return $"[{this.DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Action} " +
                $"from [{SourceFile}] to [{DestinationFile}] ({FileSize}B) in {TransferTime}ms " +
                $"encrypted in {EncryptionTime}ms";
        }

        private string XMLFormat() {
            return $"<Date>{this.DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{SaveName}</Name>" +
                $"<Action>{Action}</Action>" +
                $"<Source>{SourceFile}</Source>" +
                $"<Target>{DestinationFile}</Target>" +
                $"<FileSize>{FileSize}</FileSize>" +
                $"<TransferTime>{TransferTime}</TransferTime>" +
                $"<EncryptionTime>{EncryptionTime}</EncryptionTime>";
        }

        private string JSONFormat() {
            var JsonLog = new
            {
                Date = this.DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                Name = SaveName,
                Source = SourceFile,
                Target = DestinationFile,
                Action = this.Action,
                FileSize = this.FileSize,
                TransferTime = this.TransferTime,
                EncryptionTime = this.EncryptionTime,
            };
            return JsonSerializer.Serialize(JsonLog);
        }

        public string Format(LogFormat format) {
            switch (format) {
                case LogFormat.Text: return TextFormat();
                case LogFormat.XML: return XMLFormat();
                case LogFormat.JSON: return JSONFormat();
                default: return "";
            }
        }
    }

    // Fire-and-forget TCP client. Connects lazily; reconnects on failure.
    // Send errors are swallowed so a downed log server never blocks a save.
    internal sealed class TcpLogClient : IDisposable
    {
        private readonly object _lock = new();
        private TcpClient? _tcp;
        private StreamWriter? _writer;
        private string _host;
        private int _port;

        public TcpLogClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Reconfigure(string host, int port)
        {
            lock (_lock)
            {
                if (_host == host && _port == port) return;
                _host = host;
                _port = port;
                DropConnection();
            }
        }

        // Send without blocking the caller. If the connection fails the line is
        // silently dropped — availability of the backup is not contingent on the
        // log server being reachable.
        public void SendAsync(string line)
        {
            // Capture so the Task closure doesn't race with Reconfigure.
            string host;
            int port;
            lock (_lock) { host = _host; port = _port; }

            _ = Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        EnsureConnected(host, port);
                        _writer!.WriteLine(line);
                        _writer.Flush();
                    }
                }
                catch
                {
                    lock (_lock) { DropConnection(); }
                }
            });
        }

        private void EnsureConnected(string host, int port)
        {
            if (_tcp?.Connected == true) return;
            DropConnection();
            _tcp = new TcpClient();
            _tcp.Connect(host, port);
            _writer = new StreamWriter(_tcp.GetStream(), Encoding.UTF8) { AutoFlush = false };
        }

        private void DropConnection()
        {
            try { _writer?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _writer = null;
            _tcp = null;
        }

        public void Dispose()
        {
            lock (_lock) { DropConnection(); }
        }
    }

    public class Logger
    {
        private static Logger? s_instance;
        private static readonly object s_lock = new();

        private string _outputDir;
        private LogMode _mode = LogMode.Local;
        private TcpLogClient? _tcpClient;
        private static readonly object s_rwLock = new();

        private Logger(string outputDir)
        {
            _outputDir = outputDir;
        }

        public static Logger Get(string outputDir)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    s_instance ??= new Logger(outputDir);
                }
            }
            return s_instance;
        }

        public void Log(string log)
        {
            LogMode mode;
            TcpLogClient? tcp;
            lock (s_rwLock) { mode = _mode; tcp = _tcpClient; }

            if (mode == LogMode.Local || mode == LogMode.Both)
            {
                lock (s_rwLock)
                {
                    string path = Path.Combine(_outputDir, $"{DateTime.Now:yyyy-MM-dd}.log");
                    Directory.CreateDirectory(_outputDir);
                    using StreamWriter writer = new(path, true);
                    writer.WriteLine(log);
                }
            }

            if ((mode == LogMode.Remote || mode == LogMode.Both) && tcp != null)
            {
                tcp.SendAsync(log);
            }
        }

        public void ModifyOutput(string outputDir)
        {
            lock(s_rwLock) { _outputDir = outputDir; }
        }

        // Called by ConfigManager when the user changes log mode/endpoint.
        public void SetRemoteConfig(LogMode mode, string host, int port)
        {
            lock (s_rwLock)
            {
                _mode = mode;
                if (mode == LogMode.Local)
                {
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    return;
                }
                if (_tcpClient == null)
                    _tcpClient = new TcpLogClient(host, port);
                else
                    _tcpClient.Reconfigure(host, port);
            }
        }
    }
}
