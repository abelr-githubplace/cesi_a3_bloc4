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

        private const int CopyBufferSize = 64 * 1024;

        protected static long CopyChunked(string from, string to, CancellationToken token, Action<long> reportProgress)
        {
            if (!File.Exists(from)) return 0;

            string? toDir = Path.GetDirectoryName(to);
            if (string.IsNullOrEmpty(toDir)) return 0;
            if (!Directory.Exists(toDir)) Directory.CreateDirectory(toDir);

            long totalCopied = 0;
            try
            {
                using var src = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var dst = new FileStream(to, FileMode.Create, FileAccess.Write);

                var buffer = new byte[CopyBufferSize];
                int read;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    dst.Write(buffer, 0, read);
                    totalCopied += read;
                    reportProgress(read);
                }
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(to)) File.Delete(to); } catch { }
                throw;
            }

            return totalCopied;
        }

        public abstract long Execute(CancellationToken token, Action<long> reportProgress);
    }

    public class CompleteSaveFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute(CancellationToken token, Action<long> reportProgress)
            => CopyChunked(SourceFile, DestinationFile, token, reportProgress);
    }

    public class DifferentialSaveFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute(CancellationToken token, Action<long> reportProgress)
        {
            if (!File.Exists(SourceFile)) return CopyChunked(SourceFile, DestinationFile, token, reportProgress);
            if (!File.Exists(DestinationFile)) return CopyChunked(SourceFile, DestinationFile, token, reportProgress);

            token.ThrowIfCancellationRequested();
            byte[] srcBytes = File.ReadAllBytes(SourceFile);
            token.ThrowIfCancellationRequested();
            byte[] dstBytes = File.ReadAllBytes(DestinationFile);
            string srcHash = XDelta.XDelta.ComputeSha256(srcBytes);
            string dstHash = XDelta.XDelta.ComputeSha256(dstBytes);

            if (srcHash == dstHash) { reportProgress(FileSize); return FileSize; }
            return CopyChunked(SourceFile, DestinationFile, token, reportProgress);
        }
    }

    public class DeltaSaveFileJob(string sourceFile, string destinationFile, string diffFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute(CancellationToken token, Action<long> reportProgress)
        {
            if (!File.Exists(SourceFile)) return CopyChunked(SourceFile, DestinationFile, token, reportProgress);
            if (!File.Exists(DestinationFile)) return CopyChunked(SourceFile, DestinationFile, token, reportProgress);

            token.ThrowIfCancellationRequested();
            byte[] srcBytes = File.ReadAllBytes(SourceFile);
            token.ThrowIfCancellationRequested();
            byte[] dstBytes = File.ReadAllBytes(DestinationFile);
            string srcHash = XDelta.XDelta.ComputeSha256(srcBytes);
            string dstHash = XDelta.XDelta.ComputeSha256(dstBytes);

            if (srcHash == dstHash) { reportProgress(FileSize); return FileSize; }

            token.ThrowIfCancellationRequested();
            byte[] diffBytes = XDelta.XDelta.Encode(srcBytes, dstBytes);
            File.WriteAllBytes(diffFile, diffBytes);
            reportProgress(FileSize);
            return FileSize;
        }
    }

    public class CompleteRestoreFileJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute(CancellationToken token, Action<long> reportProgress)
            => CopyChunked(DestinationFile, SourceFile, token, reportProgress);
    }

    public class DifferentialRestoreFileJob(string sourceFile, string destinationFile, string diffFile, long fileSize, Priority priority)
        : FileJob(sourceFile, destinationFile, fileSize, priority)
    {
        public override long Execute(CancellationToken token, Action<long> reportProgress)
        {
            if (!File.Exists(DestinationFile)) return CopyChunked(DestinationFile, SourceFile, token, reportProgress);
            if (!File.Exists(SourceFile)) return CopyChunked(DestinationFile, SourceFile, token, reportProgress);

            token.ThrowIfCancellationRequested();
            byte[] srcBytes = File.ReadAllBytes(SourceFile);
            token.ThrowIfCancellationRequested();
            byte[] dstBytes = File.ReadAllBytes(DestinationFile);
            string srcHash = XDelta.XDelta.ComputeSha256(srcBytes);
            string dstHash = XDelta.XDelta.ComputeSha256(dstBytes); // FIXME: should be stored instead

            if (srcHash == dstHash) { reportProgress(FileSize); return FileSize; }

            token.ThrowIfCancellationRequested();
            byte[] diffBytes = File.ReadAllBytes(diffFile);
            srcBytes = XDelta.XDelta.Decode(dstBytes, diffBytes);
            File.WriteAllBytes(SourceFile, srcBytes);
            reportProgress(FileSize);
            return FileSize;
        }
    }
}
