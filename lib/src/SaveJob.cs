using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

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
        public string Name { get; private set; }
        public string SourcePath { get; private set; }
        public string DestinationPath { get; private set; }
        protected ulong FileSize;
        protected Priority Priority;
        protected long TotalBytesToCopy;
        protected long CopiedBytes;
        public SaveProgress Progress { get; protected set; }

        protected SaveJob(string name, string source, string destination)
        {
            Name = name;
            SourcePath = source;
            DestinationPath = destination;
            Progress = new SaveProgress();
        }

        public float GetProgress()
        {
            return Progress.GetProgress();
        }

        public long Execute()
        {
            TotalBytesToCopy = GetTotalBytesToCopy();
            CopiedBytes = 0;
            Progress.SetProgress(0f);

            ulong copiedSize = CopyFiles();

            if (copiedSize != TotalBytesToCopy)
            {
                throw new Exception($"Erreur de sauvegarde : {TotalBytesToCopy} octets attendus, mais {copiedSize} octets copiés.");
            }

            return copiedSize;
        }

        protected ulong GetTotalBytesToCopy()
        {
            ulong totalSize = 0;
            var files = GetFilesToCopy();

            if (files != null)
            {
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists)
                    {
                        totalSize += (ulong)fileInfo.Length;
                    }
                }
            }

            return totalSize;
        }

        public abstract IEnumerable<string> GetFilesToCopy();
        protected abstract long CopyFiles();

        protected long CopyFile(string sourceFilePath, string targetPath)
        {
            if (!File.Exists(sourceFilePath)) return 0;

            string destFile = Path.Combine(targetPath, Path.GetRelativePath(SourcePath, sourceFilePath));
            string destDir = Path.GetDirectoryName(destFile);

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourceFilePath, destFile, true);
            long copiedSize = new FileInfo(destFile).Length;

            CopiedBytes += copiedSize;
            float percent = TotalBytesToCopy == 0 ? 100f : ((float)CopiedBytes / TotalBytesToCopy) * 100f;
            Progress.SetProgress(percent);

            return copiedSize;
        }
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string name, string source, string destination)
            : base(name, source, destination)
        {
        }

        public override IEnumerable<string> GetFilesToCopy()
        {
            var dir = new DirectoryInfo(SourcePath);
            return dir.GetFiles("*", SearchOption.AllDirectories).Select(f => f.FullName).ToList();
        }

        protected override ulong CopyFiles()
        {
            ulong copied = 0;
            var files = GetFilesToCopy();
            foreach (var file in files)
            {
                copied += CopyFile(file, DestinationPath);
            }
            return copied;
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        public DifferentialSaveJob(string name, string source, string destination)
            : base(name, source, destination)
        {
        }

        public override IEnumerable<string> GetFilesToCopy()
        {
            var sourceDir = new DirectoryInfo(SourcePath);
            var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);

            return files.Where(f => {
                string relativePath = Path.GetRelativePath(SourcePath, f.FullName);
                string destFilePath = Path.Combine(DestinationPath, relativePath);   
                if (!File.Exists(destFilePath)) return true;
                return f.LastWriteTime > File.GetLastWriteTime(destFilePath);
            }).Select(f => f.FullName).ToList();
        }

        protected override long CopyFiles()
        {
            long copied = 0;
            var files = GetFilesToCopy();
            foreach (var file in files)
            {
                copied += CopyFile(file, DestinationPath);
            }
            return copied;
        }
    }
}