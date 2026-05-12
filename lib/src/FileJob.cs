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

        // V3 Stop is immediate: a 5 GB copy must react to cancellation, not
        // wait for File.Copy to return 30 seconds later. CopyChunked reads/
        // writes 64 KB at a time and checks the token between every chunk,
        // so the longest a worker can "ignore" a Stop is the time of one
        // chunk transfer (negligible on local/SMB drives).
        //
        // On cancellation the partial destination file is removed, so a
        // resumed save (future feature) or the user inspecting the target
        // doesn't find a corrupted half-copy.
        private const int CopyBufferSize = 64 * 1024;

        // reportProgress is invoked with the number of bytes written by the
        // current chunk (NOT a running total), so the caller can keep a
        // cumulative counter via Interlocked.Add without double-counting.
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
                // Don't leave a half-copied artifact at the destination.
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

    public class DifferentialSaveFileJob(string sourceFile, string destinationFile, string diffFile, long fileSize, Priority priority)
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
            string dstHash = XDelta.XDelta.ComputeSha256(dstBytes); // FIXME: should be stored instead

            // No diff was needed: report the FileSize so the caller's
            // cumulative counter still moves forward.
            if (srcHash == dstHash) { reportProgress(FileSize); return FileSize; }

            token.ThrowIfCancellationRequested();
            byte[] diffBytes = XDelta.XDelta.Encode(srcBytes, dstBytes);
            File.WriteAllBytes(diffFile, diffBytes);
            // Diff path is not chunked, so report the whole file at once.
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
