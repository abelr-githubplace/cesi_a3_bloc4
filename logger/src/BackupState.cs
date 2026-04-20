using System;

namespace EasyLog
{
    public class BackupState
    {
        public string BackupName { get; set; }
        public DateTime LastActionTime { get; set; }
        public string Status { get; set; }  // Active or / Inactive

        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }

        public int FilesRemaining { get; set; }
        public long SizeRemaining { get; set; }

        public int Progress { get; set; } // en %

        public string CurrentSourceFile { get; set; }
        public string CurrentTargetFile { get; set; }
    }
}