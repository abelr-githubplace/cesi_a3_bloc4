using System.Security.Cryptography;
using XDelta;

namespace Job
{
    public enum Priority { High, Low }

    public abstract class FileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
    {
        public string SourceFile { get; protected set; } = sourceFile;
        public string DestinationFile { get; protected set; } = destinationFile;
        public long FileSize { get; protected set; } = fileSize;
        public Priority Priority { get; protected set; } = priority;

        protected static (bool, byte[], byte[]) IsIdentical(string from, string to)
        {
            byte[] srcBytes = File.ReadAllBytes(from);
            byte[] dstBytes = File.ReadAllBytes(to);
            string srcHash = XDelta.XDelta.ComputeSha256(srcBytes);
            string dstHash = XDelta.XDelta.ComputeSha256(dstBytes);
            return (srcHash == dstHash, srcBytes, dstBytes);
        }

        protected static long Copy(string from, string to)
        {
            if (!File.Exists(from)) return 0;
            
            string? toDir = Path.GetDirectoryName(to);
            if (string.IsNullOrEmpty(toDir)) return 0;
            if (!Directory.Exists(toDir)) Directory.CreateDirectory(toDir);

            File.Copy(from, to, true);
            return new FileInfo(to).Length;
        }

        public abstract long Execute();
    }

    public class RawSaveFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute() => Copy(SourceFile, DestinationFile);
    }

    public class CompleteSaveFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute()
        {
            (bool identical, _, _) = IsIdentical(SourceFile, DestinationFile);
            if (identical) return FileSize;
            return Copy(SourceFile, DestinationFile);
        }
    }

    public class DifferentialSaveFileJob(string sourceFile, string destinationFile, string diffFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute()
        {
            if (!File.Exists(SourceFile)) return Copy(SourceFile, DestinationFile);
            if (!File.Exists(DestinationFile)) return Copy(SourceFile, DestinationFile);
            (bool identical, byte[] srcBytes, byte[] dstBytes) = IsIdentical(SourceFile, DestinationFile);
            if (identical) return FileSize;

            byte[] diffBytes = XDelta.XDelta.Encode(srcBytes, dstBytes);
            File.WriteAllBytes(diffFile, diffBytes);
            return FileSize;
        }
    }

    public class CompleteRestoreFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute()
        {
            (bool identical, _, _) = IsIdentical(SourceFile, DestinationFile);
            if (identical) return FileSize;
            return Copy(DestinationFile, SourceFile);
        }
    }

    public class DifferentialRestoreFileJob(string sourceFile, string destinationFile, string diffFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute()
        {
            if (!File.Exists(DestinationFile)) return Copy(DestinationFile, SourceFile);
            if (!File.Exists(SourceFile)) return Copy(DestinationFile, SourceFile);
            (bool identical, _, byte[] dstBytes) = IsIdentical(SourceFile, DestinationFile);
            if (identical) return FileSize;

            byte[] diffBytes = File.ReadAllBytes(diffFile);
            byte[] srcBytes = XDelta.XDelta.Decode(dstBytes, diffBytes);
            File.WriteAllBytes(SourceFile, srcBytes);
            return FileSize;
        }
    }
}