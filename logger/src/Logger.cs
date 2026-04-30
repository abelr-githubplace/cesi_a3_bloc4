using System;
using System.IO;

namespace EasyLog
{
    public enum LogFormat {
        Text,
        JSON,
        XML,
    }

    public record LogInfo
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required string Action { get; init; }
        public required long FileSize { get; init; }
        public required int TransferTime { get; init; }
        public required int CryptoTime { get; init; }

        private string TextFormat() {
            return $"[{this.DateTime:dd-MM-yyyy HH:mm:ss}] " +
                $"{SaveName} > {Action} from [{SourceFile}] to [{DestinationFile}] ({FileSize}kB) in {TransferTime}ms";
        }

        private string XMLFormat() {
            return $"<Date>{this.DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{SaveName}</Name>" +
                $"<Action>{Action}</Action>" +
                $"<Source>{SourceFile}</Source>" +
                $"<Target>{DestinationFile}</Target>" +
                $"<SizeKB>{FileSize}</SizeKB>" +
                $"<TransferTimeMS>{TransferTime}</TransferTimeMS>" +
                $"<CryptoTimeMS>{CryptoTime}</CryptoTimeMS>";
        }

        public string Format(LogFormat format) {
            switch (format) {
                case LogFormat.Text: return TextFormat();
                case LogFormat.XML: return XMLFormat();
                default: break;
            }
        }
    }

    public class Logger
    {
        private static Logger s_instance;
        private static readonly object s_lock = new object();

        private readonly string _outputFile;

        private Logger(string outputFile)
        {
            _outputFile = outputFile;
        }

        public static Logger Get(string outputFile, LogFormat format = LogFormat.Text)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null) s_instance = new Logger(outputFile, format);
                }
            }
            return s_instance;
        }

        public void Log(string log)
        {
            using (StreamWriter writer = new StreamWriter(_outputFile, true))
            {
                writer.WriteLine(log);
            }
        }
    }
}