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
        private const int _diff = 0x46464944; // "DIFF"

        // xdelta-style rolling-hash parameters.
        // BlockSize is the granularity at which we look for matches. Smaller values
        // catch finer matches (better diff) at the cost of a bigger index. 16 is a
        // good general-purpose default; bump it up for huge files if memory matters.
        private const int _blockSize = 16;
        // Cap how many candidate positions per hash bucket we'll try to extend.
        // On highly repetitive data (e.g. zero-padded files) a single hash can map
        // to millions of positions, which would make the inner loop quadratic.
        // 32 is enough to find a good match in practice.
        private const int _maxCandidatesPerHash = 32;
        // Rabin-Karp polynomial base. 257 is a small prime > 256 so every byte
        // value contributes uniquely to the hash before reduction.
        private const uint _hashBase = 257;

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

        private enum OpTag : byte { Copy = 0, Add = 1 }

        private struct Op
        {
            public OpTag Tag;
            public int Offset; // Copy: position in old file. Add: position in new file (only used at write time).
            public int Length;
        }

        // .diff format (little-endian):
        //   int   magic ("DIFF")
        //   int   oldFileLength
        //   int   newFileLength
        //   int   opCount
        //   for each op:
        //     byte tag (0 = Copy, 1 = Add)
        //     if Copy:
        //       int srcOffset   // start position in old file
        //       int length
        //     if Add:
        //       int length
        //       byte[length]    // literal bytes
        //
        // Reconstruction: walk ops in order, appending either old[srcOffset..srcOffset+length]
        // (Copy) or the literal bytes (Add). This is xdelta/VCDIFF-style: a Copy can
        // reference *any* part of the old file, so a moved block is encoded as a single
        // Copy regardless of how far it travelled.
        //
        // The encoder builds an index of every BlockSize-byte window of the old file
        // (Rabin-Karp rolling hash), then scans the new file with the same rolling hash:
        // on a hash hit, it verifies the actual bytes (defending against collisions)
        // and extends the match forward as far as possible. Bytes that don't anchor a
        // match accumulate in a literal buffer that's flushed as an Add op when the
        // next Copy starts (or at end of file). Both pre-indexing and the scan are
        // O(N+M) on average, so this scales to large files.
        public static void GenerateDelta(string sourceFile, string destinationFile, string diffFile)
        {
            byte[] oldBytes = File.ReadAllBytes(destinationFile);
            byte[] newBytes = File.ReadAllBytes(sourceFile);

            var ops = ComputeDelta(oldBytes, newBytes);

            using (var diffStream = File.Create(diffFile))
            using (var writer = new BinaryWriter(diffStream))
            {
                writer.Write(_diff);
                writer.Write(oldBytes.Length);
                writer.Write(newBytes.Length);
                writer.Write(ops.Count);
                foreach (var op in ops)
                {
                    writer.Write((byte)op.Tag);
                    if (op.Tag == OpTag.Copy)
                    {
                        writer.Write(op.Offset);
                        writer.Write(op.Length);
                    }
                    else
                    {
                        writer.Write(op.Length);
                        writer.Write(newBytes, op.Offset, op.Length);
                    }
                }
            }
        }

        private static List<Op> ComputeDelta(byte[] oldBytes, byte[] newBytes)
        {
            var ops = new List<Op>();
            int B = _blockSize;

            // Degenerate cases: at least one file shorter than a block — no point
            // building an index, just emit the whole new file (if any) as a literal.
            if (oldBytes.Length < B || newBytes.Length < B)
            {
                if (newBytes.Length > 0)
                    ops.Add(new Op { Tag = OpTag.Add, Offset = 0, Length = newBytes.Length });
                return ops;
            }

            // Precompute base^(B-1) for the rolling-hash update.
            uint basePow = 1;
            for (int i = 0; i < B - 1; i++) basePow *= _hashBase;

            // Index every B-byte window of the old file by its hash.
            var index = new Dictionary<uint, List<int>>();
            uint h = InitHash(oldBytes, 0, B);
            AddCandidate(index, h, 0);
            for (int i = 1; i + B <= oldBytes.Length; i++)
            {
                h = RollHash(h, oldBytes[i - 1], oldBytes[i + B - 1], basePow);
                AddCandidate(index, h, i);
            }

            // Scan the new file with the same rolling hash, looking for matches.
            int newPos = 0;
            int literalStart = 0;
            uint hn = InitHash(newBytes, 0, B);

            while (newPos + B <= newBytes.Length)
            {
                int bestSrc = -1;
                int bestLen = 0;
                if (index.TryGetValue(hn, out var positions))
                {
                    foreach (int p in positions)
                    {
                        // Verify (hash collisions exist) and greedily extend forward.
                        int len = ExtendMatch(oldBytes, p, newBytes, newPos);
                        if (len > bestLen) { bestLen = len; bestSrc = p; }
                    }
                }

                if (bestLen >= B)
                {
                    if (newPos > literalStart)
                        ops.Add(new Op
                        {
                            Tag = OpTag.Add,
                            Offset = literalStart,
                            Length = newPos - literalStart,
                        });
                    ops.Add(new Op { Tag = OpTag.Copy, Offset = bestSrc, Length = bestLen });
                    newPos += bestLen;
                    literalStart = newPos;

                    if (newPos + B <= newBytes.Length)
                        hn = InitHash(newBytes, newPos, B);
                    else
                        break;
                }
                else
                {
                    // Slide the window by one byte. We can only roll if the byte
                    // entering the new window is in bounds.
                    if (newPos + B < newBytes.Length)
                        hn = RollHash(hn, newBytes[newPos], newBytes[newPos + B], basePow);
                    newPos++;
                }
            }

            // Anything left over (the trailing bytes that couldn't form a full block,
            // plus any literal we accumulated) gets emitted as one final Add.
            if (newBytes.Length > literalStart)
                ops.Add(new Op
                {
                    Tag = OpTag.Add,
                    Offset = literalStart,
                    Length = newBytes.Length - literalStart,
                });

            return ops;
        }

        private static uint InitHash(byte[] data, int start, int len)
        {
            uint h = 0;
            for (int i = 0; i < len; i++) h = h * _hashBase + data[start + i];
            return h;
        }

        private static uint RollHash(uint hash, byte leaving, byte entering, uint basePow)
        {
            return (hash - leaving * basePow) * _hashBase + entering;
        }

        private static void AddCandidate(Dictionary<uint, List<int>> index, uint hash, int position)
        {
            if (!index.TryGetValue(hash, out var list))
            {
                list = new List<int>(1);
                index[hash] = list;
            }
            // Keep the bucket bounded so highly repetitive data doesn't make matching
            // quadratic. We keep the earliest positions: in practice they're as good
            // as any other for finding a long match.
            if (list.Count < _maxCandidatesPerHash) list.Add(position);
        }

        private static int ExtendMatch(byte[] oldBytes, int oldStart, byte[] newBytes, int newStart)
        {
            int len = 0;
            int oldRem = oldBytes.Length - oldStart;
            int newRem = newBytes.Length - newStart;
            int max = oldRem < newRem ? oldRem : newRem;
            while (len < max && oldBytes[oldStart + len] == newBytes[newStart + len]) len++;
            return len;
        }

        public override long Execute()
        {
            if (!File.Exists(DestinationFile)) return CopyFile();

            string sourceHash = ComputeSha256(SourceFile);
            string destHash = ComputeSha256(DestinationFile);

            if (sourceHash == destHash) return FileSize;

            GenerateDelta(SourceFile, DestinationFile, DestinationFile + ".diff");
            return FileSize;
        }
    }
}