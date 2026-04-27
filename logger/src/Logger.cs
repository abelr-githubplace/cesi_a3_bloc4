using System;
using System.IO;

namespace EasyLog
{
    public record LogInfo
    {
        public required DateTime DateTime { get; init; }
        public required string SaveName { get; init; }
        public required string SourceFile { get; init; }
        public required string DestinationFile { get; init; }
        public required long FileSize { get; init; }
        public required int TransferTime { get; init; }
    }

    public class Logger
    {
        private static Logger s_instance;
        private static readonly object s_lock = new object();

        private readonly string _outputFile;
        private readonly LogFormat _format;

        private Logger(string outputFile, LogFormat format)
        {
            _outputFile = outputFile;
            _format = format;
        }

        public static Logger Get(string outputFile, LogFormat format = LogFormat.Text)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new Logger(outputFile, format);
                    }
                }
            }
            return s_instance;
        }

        public void Log(LogInfo logInfo)
        {
            if (_format == LogFormat.Xml)
            {
                WriteXml(logInfo);
            }
            else
            {
                WriteText(logInfo);
            }
        }

        //  FORMAT TEXTE (par défaut)
        private void WriteText(LogInfo logInfo)
        {
            using StreamWriter writer = new StreamWriter(_outputFile, true);

            string line =
                $"[{logInfo.DateTime:dd-MM-yyyy HH:mm:ss}] " +
                $"{logInfo.SaveName} > SAVE from " +
                $"[{logInfo.SourceFile}] to " +
                $"[{logInfo.DestinationFile}] " +
                $"({logInfo.FileSize}kB) in {logInfo.TransferTime}ms";

            writer.WriteLine(line);
        }

        // FORMAT XML 
        private void WriteXml(LogInfo logInfo)
        {
            using StreamWriter writer = new StreamWriter(_outputFile, true);

            writer.WriteLine(
                $"<Date>{logInfo.DateTime:dd-MM-yyyy HH:mm:ss}</Date>" +
                $"<Name>{logInfo.SaveName}</Name>" +
                $"<Source>{logInfo.SourceFile}</Source>" +
                $"<Target>{logInfo.DestinationFile}</Target>" +
                $"<SizeKB>{logInfo.FileSize}</SizeKB>" +
                $"<TransferTimeMS>{logInfo.TransferTime}</TransferTimeMS>"
            );
        }

        // Close volontairement 
        public void Close()
        {
            // format journal sans racine
        }
    }
}