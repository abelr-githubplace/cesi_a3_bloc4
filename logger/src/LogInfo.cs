using System;

namespace EasyLog
{
    public record LogInfo
    {
        public DateTime DateTime { get; init; }
        public string SaveName { get; init; }
        public string SourceFile { get; init; }
        public string DestinationFile { get; init; }
        public int FileSize { get; init; }
        public int TransferTime { get; init; } // ms
    }
}