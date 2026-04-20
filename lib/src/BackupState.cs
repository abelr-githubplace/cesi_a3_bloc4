using System;

namespace EasyLog
{
    public record BackupState
    {
        public string BackupName { get; init; }
        public DateTime LastActionTime { get; init; }
        public string Status { get; init; } // Active / Inactive

        public int TotalFiles { get; init; }
        public long TotalSize { get; init; }

        public int FilesRemaining { get; init; }
        public long SizeRemaining { get; init; }

        public int Progress { get; init; } // %

        public string CurrentSourceFile { get; init; }
        public string CurrentTargetFile { get; init; }
    }
}
