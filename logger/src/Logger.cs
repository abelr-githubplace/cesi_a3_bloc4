using System;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public enum LogFormat { Text, JSON, XML }

    public record LogInfo
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required string Action { get; init; }
        public required long FileSize { get; init; }        // bytes
        public required int TransferTime { get; init; }     // milliseconds
        // 0 = not encrypted, >0 = encryption time in ms, <0 = error code
        public int EncryptionTime { get; init; } = 0;

        private string TextFormat() {
            return $"[{this.DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Action} " +
                $"from [{SourceFile}] to [{DestinationFile}] ({FileSize}B) in {TransferTime}ms" +
                $" [crypt:{EncryptionTime}ms]";
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

    public class Logger
    {
        private static Logger? s_instance;
        private static readonly object s_lock = new object();

        // Cahier des charges: daily log file (one rotation per day). The
        // constructor takes a directory; the actual file name is computed at
        // every Log() call from today's date, so a long-running app crosses
        // midnight cleanly without restart.
        private readonly string _directory;
        private readonly object _writeLock = new object();

        private Logger(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public static Logger Get(string directory)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null) s_instance = new Logger(directory);
                }
            }
            return s_instance;
        }

        public string CurrentLogFile => Path.Combine(_directory, $"{DateTime.Now:yyyy-MM-dd}.log");

        public void Log(string log)
        {
            lock (_writeLock)
            {
                using (StreamWriter writer = new StreamWriter(CurrentLogFile, true))
                {
                    writer.WriteLine(log);
                }
            }
        }
    }
}