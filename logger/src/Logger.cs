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

    
        public int CryptoTime { get; init; }

        //  V2.0 
        public string? SystemMessage { get; init; }

       
        // V1.1 : formats existants
       
        private string TextFormat()
        {
            return $"[{DateTime:dd-MM-yyyy HH:mm:ss}] {SaveName} > {Action} " +
                   $"from [{SourceFile}] to [{DestinationFile}] " +
                   $"({FileSize}B) in {TransferTime}ms" +
                   $" | CryptoTime: {CryptoTime}ms";
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
                   $"<CryptoTime>{CryptoTime}</CryptoTime>";
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
                CryptoTime
            };
            return JsonSerializer.Serialize(jsonLog);
        }

        //  V1.1 conservé
        public string Format(LogFormat format)
        {
            return format switch
            {
                LogFormat.Text => TextFormat(),
                LogFormat.XML => XMLFormat(),
                LogFormat.JSON => JSONFormat(),
                _ => ""
            };
        }
    }

    // LOGGER V1.1 + V2
   
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

        public void Log(LogInfo logInfo, LogFormat format)
        {
            using StreamWriter writer = new StreamWriter(_outputFile, true);

            //  arrêt logiciel métier
            if (!string.IsNullOrEmpty(logInfo.SystemMessage))
            {
                writer.WriteLine(
                    format == LogFormat.JSON
                        ? JsonSerializer.Serialize(new
                        {
                            Date = logInfo.DateTime.ToString("dd-MM-yyyy HH:mm:ss"),
                            Event = "STOP",
                            Message = logInfo.SystemMessage
                        })
                        : $"<Date>{logInfo.DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                          $"<Event>STOP</Event>" +
                          $"<Message>{logInfo.SystemMessage}</Message>"
                );
                return;
            }

            // sauvegarde 
            writer.WriteLine(logInfo.Format(format));
        }
    }
}
