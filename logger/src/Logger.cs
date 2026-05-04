using System;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public enum LogFormat { Text, JSON, XML }

    public record LogSaveInterrup
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string Interrupt { get; init; }

        private string TextFormat()
        {
            return $"[{DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Interrupt}";
        }

        private string JSONFormat()
        {
            var jsonLog = new
            {
                Date = DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                Name = SaveName,
                Interrupt,
            };
            return JsonSerializer.Serialize(jsonLog);
        }

        private string XMLFormat()
        {
            return $"<Date>{DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{SaveName}</Name>" +
                $"<Interrupt>{Interrupt}</Interrupt>";
        }

        public string Format(LogFormat format)
        {
            switch (format)
            {
                case LogFormat.Text: return TextFormat();
                case LogFormat.XML: return XMLFormat();
                case LogFormat.JSON: return JSONFormat();
                default: return JSONFormat(); // default to JSON
            }
        }
    }

    public record LogInfo
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required string Action { get; init; }
        public required long FileSize { get; init; }        // bytes
        public required int TransferTime { get; init; }     // milliseconds
        public required int EncryptionTime { get; init; }   // milliseconds
       
        private string TextFormat()
        {
            return $"[{DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Action} " +
                $"from [{SourceFile}] to [{DestinationFile}] " +
                $"({FileSize}B) in {TransferTime}ms " +
                $"encrypted in {EncryptionTime}ms";
        }

        private string XMLFormat()
        {
            return $"<Date>{DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{SaveName}</Name>" +
                $"<Action>{Action}</Action>" +
                $"<Source>{SourceFile}</Source>" +
                $"<Target>{DestinationFile}</Target>" +
                $"<FileSize>{FileSize}</FileSize>" +
                $"<TransferTime>{TransferTime}</TransferTime>" +
                $"<EncryptionTime>{EncryptionTime}</EncryptionTime>";
        }

        private string JSONFormat()
        {
            var jsonLog = new
            {
                Date = DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                Name = SaveName,
                Action,
                Source = SourceFile,
                Target = DestinationFile,
                FileSize,
                TransferTime,
                EncryptionTime,
            };
            return JsonSerializer.Serialize(jsonLog);
        }

        public string Format(LogFormat format)
        {
            switch (format)
            {
                case LogFormat.Text: return TextFormat();
                case LogFormat.XML: return XMLFormat();
                case LogFormat.JSON: return JSONFormat();
                default: return JSONFormat(); // default to JSON
            }
        }
    }
   
    public class Logger
    {
        private static Logger? s_instance;
        private static readonly object s_lock = new();

        private readonly string _outputFile;

        private Logger(string outputFile)
        {
            _outputFile = outputFile;
        }

        public static Logger Get(string outputFile)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    s_instance ??= new Logger(outputFile);
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
