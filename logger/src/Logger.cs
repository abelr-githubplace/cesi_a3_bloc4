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
        public required int TransferTime { get; init; } // ms
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

        public static Logger Get(string outputFile)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new Logger(outputFile);
                    }
                }
            }
            return s_instance;
        }

        public void Log(LogInfo logInfo)
        {
            using (StreamWriter writer = new StreamWriter(_outputFile, true))
            {
                string line =
                    $"[{logInfo.DateTime:dd-MM-yyyy HH:mm:ss}] " +
                    $"{logInfo.SaveName} > SAVE from " +
                    $"[{logInfo.SourceFile}] to " +
                    $"[{logInfo.DestinationFile}] " +
                    $"({logInfo.FileSize}kB) in {logInfo.TransferTime}ms";

                writer.WriteLine(line);
            }
        }
    }
}
