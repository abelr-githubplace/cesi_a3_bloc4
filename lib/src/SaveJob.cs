using System;
using System.IO;
using Logger;

namespace Save
{
    public enum Priority
    {
        High,
        Medium,
        Low
    }

    public abstract class SaveJob
    {
        public string SourceFile { get; private set; }
        public string DestinationFile { get; private set; }
        public long FileSize { get; private set; }
        public Priority Priority { get; private set; }

        protected SaveJob(string sourceFile, string destinationFile, long fileSize)
        {
            SourceFile = sourceFile;
            DestinationFile = destinationFile;
            FileSize = fileSize;
            Priority = Priority.Medium; 
        }

        public long Execute()
        {
            return CopyFiles();
        }

        protected virtual long CopyFiles()
        {
            if (!File.Exists(SourceFile)) return 0;

            string destDir = Path.GetDirectoryName(DestinationFile);

            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(SourceFile, DestinationFile, true);
            return new FileInfo(DestinationFile).Length;
        }
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string sourceFile, string destinationFile, long fileSize)
            : base(sourceFile, destinationFile, fileSize)
        {
        }

        protected override long CopyFiles()
        {
            return base.CopyFiles();
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        public DifferentialSaveJob(string sourceFile, string destinationFile, long fileSize)
            : base(sourceFile, destinationFile, fileSize)
        {
        }

        protected override long CopyFiles()
        {
            if (File.Exists(DestinationFile))
            {
                if (File.GetLastWriteTime(SourceFile) <= File.GetLastWriteTime(DestinationFile))
                {
                    return FileSize;
                }
            }

            return base.CopyFiles();
        }
    }
}