using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyLog
{
    public enum LogFormat { Text, JSON, XML }
    public abstract record IFormat
    {
        protected abstract string TextFormat();
        protected abstract string XMLFormat();
        protected abstract string JSONFormat();

        public string Format(LogFormat format)
        {
            return format switch
            {
                LogFormat.Text => TextFormat(),
                LogFormat.XML => XMLFormat(),
                LogFormat.JSON => JSONFormat(),
                _ => "",
            };
        }
    }

    public sealed record LogBusinessSoftwareInterrupt : IFormat
    {
        public required DateTime DateTime { get; init; }
        public required string[] Softwares { get; init; }

        protected override string TextFormat()
        {
            string soft = Softwares.Length switch
            {
                (var len) when len > 1 => $"{string.Join(" and ", Softwares)} are running : Pause all jobs",
                (var len) when len > 0 => $"{Softwares[0]} is running : Pause all jobs",
                _ => "No business software detected : Resume all jobs",
            };
            return $"[{this.DateTime:dd-MM-yyyy HH:mm:ss}] {soft}";
        }

        protected override string XMLFormat()
        {
            string soft = Softwares.Length switch
            {
                (var len) when len > 1 => $"{string.Join(" and ", Softwares)} are running",
                (var len) when len > 0 => $"{Softwares[0]} is running",
                _ => "No business software detected",
            };
            return $"<Date>{this.DateTime:dd-MM-yyyy HH:mm:ss}</Date><BusinessSoftware>{soft}</BusinessSoftware>";
        }

        protected override string JSONFormat()
        {
            var JsonLog = new
            {
                Date = this.DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                BusinessSoftware = Softwares.Length switch {
                    (var len) when len > 1 => $"{string.Join(" and ", Softwares)} are running",
                    (var len) when len > 0 => $"{Softwares[0]} is running",
                    _ => "No business software detected",
                },
            };
            return JsonSerializer.Serialize(JsonLog);
        }
    }

    public sealed record LogInfo : IFormat
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required string Action { get; init; }
        public required long FileSize { get; init; }        // bytes
        public required int TransferTime { get; init; }     // milliseconds
        public required int EncryptionTime { get; init; }     // milliseconds

        protected override string TextFormat() {
            return $"[{this.DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Action} " +
                $"from [{SourceFile}] to [{DestinationFile}] ({FileSize}B) in {TransferTime}ms " +
                $"encrypted in {EncryptionTime}ms";
        }

        protected override string XMLFormat() {
            return $"<Date>{this.DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{SaveName}</Name>" +
                $"<Action>{Action}</Action>" +
                $"<Source>{SourceFile}</Source>" +
                $"<Target>{DestinationFile}</Target>" +
                $"<FileSize>{FileSize}</FileSize>" +
                $"<TransferTime>{TransferTime}</TransferTime>" +
                $"<EncryptionTime>{EncryptionTime}</EncryptionTime>";
        }

        protected override string JSONFormat() {
            var JsonLog = new
            {
                Date = this.DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                Name = SaveName,
                Source = SourceFile,
                Target = DestinationFile,
                Action,
                FileSize,
                TransferTime,
                EncryptionTime,
            };
            return JsonSerializer.Serialize(JsonLog);
        }
    }

    public class Logger
    {
        private static Logger? s_instance;
        private static readonly object s_lock = new();

        private string _output;
        private static readonly object s_rwLock = new();

        private Logger(string output)
        {
            _output = output;
        }

        public static Logger Get(string output)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    s_instance ??= new Logger(output);
                }
            }
            return s_instance;
        }

        public void Log(string log)
        {
            lock (s_rwLock)
            {
                using StreamWriter writer = new(_output, true);
                writer.WriteLine(log);
            }
        }

        public void ModifyOutput(string output)
        {
            lock(s_rwLock) { _output = output; }
        }
    }
}