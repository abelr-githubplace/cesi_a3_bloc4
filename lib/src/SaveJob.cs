using System.Security.Cryptography;

namespace Job
{
    public enum Priority { High, Medium, Low }

    public abstract class SaveJob
    {
        public string SourceFile { get; private set; }
        public string DestinationFile { get; private set; }
        public long FileSize { get; private set; }
        public Priority Priority { get; private set; }

        protected SaveJob(string sourceFile, string destinationFile, long fileSize, Priority priority)
        {
            SourceFile = sourceFile;
            DestinationFile = destinationFile;
            FileSize = fileSize;
            Priority = priority;
        }

        protected long CopyFile()
        {
            if (!File.Exists(SourceFile)) return 0;

            // Create parent directory
            string? destDir = Path.GetDirectoryName(DestinationFile);
            if (destDir == null) return 0;
            if (destDir != null && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            File.Copy(SourceFile, DestinationFile, true);
            return new FileInfo(DestinationFile).Length;
        }

        public abstract long Execute();
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string sourceFile, string destinationFile, long fileSize, Priority priority) : base(sourceFile, destinationFile, fileSize, priority) { }

        public override long Execute()
        {
            return CopyFile();
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        private const int _diff = 0x44494646; // "DIFF"

        public DifferentialSaveJob(string sourceFile, string destinationFile, long fileSize, Priority priority) : base(sourceFile, destinationFile, fileSize, priority) { }

        private static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public static void GenerateDelta(string sourceFile, string destinationFile, string diffFile)
        {
            byte[] oldBytes = File.ReadAllBytes(destinationFile);
            byte[] newBytes = File.ReadAllBytes(sourceFile);

            using (var diffStream = File.Create(diffFile))
            using (var writer = new BinaryWriter(diffStream))
            {
                writer.Write(_diff);
                writer.Write(oldBytes.Length);

                int offset = 0;
                int blockCount = 0;
                writer.Write(blockCount); // Reserve space

                while (offset < oldBytes.Length)
                {
                    int blockStart = offset;
                    while (offset < oldBytes.Length && oldBytes[offset] == newBytes[offset]) offset++;

                    if (offset > blockStart)
                    {
                        int blockLength = offset - blockStart;
                        writer.Write(blockStart);
                        writer.Write(blockLength);
                        writer.Write(newBytes, blockStart, blockLength);
                        blockCount++;
                    }
                }

                // This is writing twice, should be optimized
                writer.Seek(0, SeekOrigin.Begin);
                writer.Write(_diff);
                writer.Write(oldBytes.Length);
                writer.Write(blockCount);
            }
        }

        public override long Execute()
        {
            if (!File.Exists(DestinationFile)) return CopyFile(); // Fallback method

            string sourceHash = ComputeSha256(SourceFile);
            string destHash = ComputeSha256(DestinationFile);

            if (sourceHash == destHash) return FileSize;

            GenerateDelta(SourceFile, DestinationFile, DestinationFile + ".diff");
            return FileSize;
        }
    }
}