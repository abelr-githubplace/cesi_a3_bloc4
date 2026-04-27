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
        public required long FileSize { get; init; }      // kB
        public required int TransferTime { get; init; }   // ms
    }

    public class Logger
    {
        private static Logger s_instance;
        private static readonly object s_lock = new object();

        private readonly string _outputFile;
        private readonly LogFormat _format;
        private bool _xmlOpened = false;

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
                WriteXml(logInfo);
            else
                WriteText(logInfo);
        }

        // FORMAT TEXTE  
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

            if (!_xmlOpened)
            {
                writer.WriteLine("<Logs>");
                _xmlOpened = true;
            }

            writer.WriteLine("  <LogEntry>");
            writer.WriteLine($"    <Date>{logInfo.DateTime:dd-MM-yyyy HH:mm:ss}</Date>");
            writer.WriteLine($"    <SaveName>{logInfo.SaveName}</SaveName>");
            writer.WriteLine($"    <Source>{logInfo.SourceFile}</Source>");
            writer.WriteLine($"    <Target>{logInfo.DestinationFile}</Target>");
            writer.WriteLine($"    <SizeKB>{logInfo.FileSize}</SizeKB>");
            writer.WriteLine($"    <TransferTimeMS>{logInfo.TransferTime}</TransferTimeMS>");
            writer.WriteLine("  </LogEntry>");
        }

        //  FERMETURE XML
        public void Close()
        {
            if (_format == LogFormat.Xml && _xmlOpened)
            {
                using StreamWriter writer = new StreamWriter(_outputFile, true);
                writer.WriteLine("</Logs>");
                _xmlOpened = false;
            }
        }
    }
}
