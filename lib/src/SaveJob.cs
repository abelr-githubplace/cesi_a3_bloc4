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
        // Magic "DIFF" : on disk = 0x44 0x49 0x46 0x46, lu en int32 LE = 0x46464944
        private const int _diff = 0x46464944;

        // Tags d'opération du flux delta
        private const byte OpCopy = 0x00; // [0x00][srcOffset:int32][length:int32]
        private const byte OpAdd  = 0x01; // [0x01][length:int32][data:length bytes]

        // Taille de bloc Xdelta (rolling-hash window)
        private const int BlockSize = 16;

        // Adler-32 modulus
        private const uint Mod = 65521u;

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

        // Adler-32 sur une fenêtre [offset, offset+length).
        private static uint Adler32(byte[] data, int offset, int length)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < length; i++)
            {
                a = (a + data[offset + i]) % Mod;
                b = (b + a) % Mod;
            }
            return (b << 16) | a;
        }

        // Slide la fenêtre Adler-32 d'un byte (out -> in).
        private static uint RollAdler32(uint hash, byte outByte, byte inByte, int blockSize)
        {
            long a = hash & 0xFFFFu;
            long b = hash >> 16;
            a = (a - outByte + inByte + Mod) % Mod;
            // Padding suffisant pour rester >= 0 avant le mod
            long pad = Mod * (long)(blockSize + 1);
            b = (b - (long)blockSize * outByte + a - 1 + pad) % Mod;
            return ((uint)b << 16) | (uint)a;
        }

        /// <summary>
        /// Génère un fichier delta entre <paramref name="destinationFile"/> (référence) et
        /// <paramref name="sourceFile"/> (cible) selon un algorithme inspiré de Xdelta :
        /// table de hash Adler-32 sur les blocs non-chevauchants de la référence, puis
        /// scan rolling-hash sur la cible avec vérification byte-à-byte et extension
        /// gloutonne du match.
        ///
        /// Format binaire :
        ///   [magic:int32 = "DIFF"]
        ///   [oldLen:int32][newLen:int32][opCount:int32]
        ///   opCount × { COPY | ADD }
        ///     COPY = [0x00][srcOffset:int32][length:int32]
        ///     ADD  = [0x01][length:int32][data:length bytes]
        /// </summary>
        public static void GenerateDelta(string sourceFile, string destinationFile, string diffFile)
        {
            byte[] oldBytes = File.ReadAllBytes(destinationFile);
            byte[] newBytes = File.ReadAllBytes(sourceFile);

            // 1. Indexer les blocs CHEVAUCHANTS de la référence par leur Adler-32
            //    (chaque offset 0..oldLen-BlockSize). Permet de trouver l'alignement
            //    optimal d'un match, pas seulement les multiples de BlockSize.
            //    On plafonne le nombre de candidats par hash pour éviter l'explosion
            //    quadratique sur du contenu très répétitif.
            const int MaxCandidatesPerHash = 16;
            var index = new Dictionary<uint, List<int>>();
            for (int i = 0; i + BlockSize <= oldBytes.Length; i++)
            {
                uint h = Adler32(oldBytes, i, BlockSize);
                if (!index.TryGetValue(h, out var list)) { list = new List<int>(); index[h] = list; }
                if (list.Count < MaxCandidatesPerHash) list.Add(i);
            }

            // 2. Scanner la cible avec rolling-hash, accumuler des opérations COPY/ADD.
            var ops = new List<(byte Tag, int A, int B, byte[]? Data)>();
            var addBuf = new List<byte>();

            int p = 0;
            bool haveHash = false;
            uint windowHash = 0;

            while (p < newBytes.Length)
            {
                bool canHash = oldBytes.Length >= BlockSize && p + BlockSize <= newBytes.Length;

                if (!canHash)
                {
                    // Trop court pour matcher : on ajoute le byte au buffer ADD.
                    addBuf.Add(newBytes[p]);
                    p++;
                    haveHash = false;
                    continue;
                }

                if (!haveHash)
                {
                    windowHash = Adler32(newBytes, p, BlockSize);
                    haveHash = true;
                }

                // Cherche le meilleur match parmi les candidats. Pour chacun on étend
                // en avant ET en arrière (Xdelta) : le préfixe du match peut empiéter
                // sur des bytes déjà rangés dans le buffer ADD, ce qui fait qu'on les
                // retire de l'ADD pour les inclure dans le COPY.
                int bestOff = -1;
                int bestFwd = 0;
                int bestBack = 0;
                if (index.TryGetValue(windowHash, out var candidates))
                {
                    foreach (int candOff in candidates)
                    {
                        // Extension forward
                        int fwd = 0;
                        while (candOff + fwd < oldBytes.Length
                               && p + fwd < newBytes.Length
                               && oldBytes[candOff + fwd] == newBytes[p + fwd])
                        {
                            fwd++;
                        }
                        if (fwd < BlockSize) continue;

                        // Extension backward, bornée par la taille du buffer ADD
                        // (= nombre de bytes "récupérables") et par candOff.
                        int back = 0;
                        int maxBack = Math.Min(addBuf.Count, candOff);
                        while (back < maxBack
                               && oldBytes[candOff - 1 - back] == newBytes[p - 1 - back])
                        {
                            back++;
                        }

                        int total = fwd + back;
                        if (total > bestFwd + bestBack)
                        {
                            bestOff = candOff;
                            bestFwd = fwd;
                            bestBack = back;
                        }
                    }
                }

                if (bestFwd >= BlockSize)
                {
                    // Récupère les bytes étendus en arrière depuis le buffer ADD.
                    if (bestBack > 0)
                        addBuf.RemoveRange(addBuf.Count - bestBack, bestBack);

                    // Flush ce qui reste en ADD avant le COPY.
                    if (addBuf.Count > 0)
                    {
                        ops.Add((OpAdd, addBuf.Count, 0, addBuf.ToArray()));
                        addBuf.Clear();
                    }
                    ops.Add((OpCopy, bestOff - bestBack, bestFwd + bestBack, null));
                    p += bestFwd;
                    haveHash = false;
                }
                else
                {
                    // Pas de match : ajoute le byte au buffer et glisse la fenêtre d'un cran.
                    byte outByte = newBytes[p];
                    addBuf.Add(outByte);
                    p++;
                    if (p + BlockSize <= newBytes.Length)
                    {
                        byte inByte = newBytes[p + BlockSize - 1];
                        windowHash = RollAdler32(windowHash, outByte, inByte, BlockSize);
                    }
                    else
                    {
                        haveHash = false;
                    }
                }
            }

            // Flush final.
            if (addBuf.Count > 0)
            {
                ops.Add((OpAdd, addBuf.Count, 0, addBuf.ToArray()));
                addBuf.Clear();
            }

            // 3. Sérialiser le delta.
            using (var diffStream = File.Create(diffFile))
            using (var writer = new BinaryWriter(diffStream))
            {
                writer.Write(_diff);
                writer.Write(oldBytes.Length);
                writer.Write(newBytes.Length);
                writer.Write(ops.Count);

                foreach (var op in ops)
                {
                    writer.Write(op.Tag);
                    if (op.Tag == OpCopy)
                    {
                        writer.Write(op.A); // srcOffset
                        writer.Write(op.B); // length
                    }
                    else // OpAdd
                    {
                        writer.Write(op.A); // length
                        writer.Write(op.Data!);
                    }
                }
            }
        }

        public override long Execute()
        {
            if (!File.Exists(DestinationFile)) return CopyFile();

            string sourceHash = ComputeSha256(SourceFile);
            string destHash = ComputeSha256(DestinationFile);

            if (sourceHash == destHash) return FileSize;

            // Snapshot the prior destination as a sidecar diff before overwriting,
            // so a future restore can reconstruct the previous version.
            GenerateDelta(SourceFile, DestinationFile, DestinationFile + ".diff");
            return CopyFile();
        }

        // Reverse of GenerateDelta: walk the ops, reconstruct the "new" bytes from
        // the "old" bytes (which live in oldFile) plus the literals stored in the
        // diff. Output is written to outFile.
        public static void ApplyDelta(string oldFile, string diffFile, string outFile)
        {
            byte[] oldBytes = File.ReadAllBytes(oldFile);
            byte[] result = ApplyDeltaBytes(oldBytes, diffFile);

            string? outDir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            File.WriteAllBytes(outFile, result);
        }

        private static byte[] ApplyDeltaBytes(byte[] oldBytes, string diffFile)
        {
            using var stream = File.OpenRead(diffFile);
            using var reader = new BinaryReader(stream);

            int magic = reader.ReadInt32();
            if (magic != _diff) throw new InvalidDataException("Not a DIFF file");

            int oldLen = reader.ReadInt32();
            int newLen = reader.ReadInt32();
            if (oldLen != oldBytes.Length)
                throw new InvalidDataException("Old file size does not match diff header");

            int opCount = reader.ReadInt32();
            var result = new byte[newLen];
            int pos = 0;

            for (int i = 0; i < opCount; i++)
            {
                byte tag = reader.ReadByte();
                if (tag == OpCopy)
                {
                    int srcOffset = reader.ReadInt32();
                    int length = reader.ReadInt32();
                    if (srcOffset < 0 || length < 0 || srcOffset + length > oldBytes.Length)
                        throw new InvalidDataException("Copy op out of bounds");
                    if (pos + length > newLen)
                        throw new InvalidDataException("Copy op overruns output");
                    Array.Copy(oldBytes, srcOffset, result, pos, length);
                    pos += length;
                }
                else if (tag == OpAdd)
                {
                    int length = reader.ReadInt32();
                    if (pos + length > newLen)
                        throw new InvalidDataException("Add op overruns output");
                    int read = reader.Read(result, pos, length);
                    if (read != length)
                        throw new InvalidDataException("Diff truncated mid-Add");
                    pos += length;
                }
                else
                {
                    throw new InvalidDataException("Unknown op tag");
                }
            }

            if (pos != newLen)
                throw new InvalidDataException("Ops did not reconstruct the full output");

            return result;
        }
    }

    public abstract class RestoreJob
    {
        public string SourceFile { get; private set; }      // original location to restore TO
        public string DestinationFile { get; private set; } // backup file to restore FROM
        public long FileSize { get; private set; }

        protected RestoreJob(string sourceFile, string destinationFile, long fileSize)
        {
            SourceFile = sourceFile;
            DestinationFile = destinationFile;
            FileSize = fileSize;
        }

        protected long CopyBack()
        {
            if (!File.Exists(DestinationFile)) return 0;

            string? srcDir = Path.GetDirectoryName(SourceFile);
            if (!string.IsNullOrEmpty(srcDir) && !Directory.Exists(srcDir))
                Directory.CreateDirectory(srcDir);

            File.Copy(DestinationFile, SourceFile, true);
            return new FileInfo(SourceFile).Length;
        }

        public abstract long Execute();
    }

    public class CompleteRestoreJob : RestoreJob
    {
        public CompleteRestoreJob(string sourceFile, string destinationFile, long fileSize)
            : base(sourceFile, destinationFile, fileSize) { }

        public override long Execute() => CopyBack();
    }

    public class DifferentialRestoreJob : RestoreJob
    {
        private readonly string _diffFile;

        public DifferentialRestoreJob(string sourceFile, string destinationFile, string diffFile, long fileSize)
            : base(sourceFile, destinationFile, fileSize)
        {
            _diffFile = diffFile;
        }

        public override long Execute()
        {
            if (!File.Exists(_diffFile)) return CopyBack();
            DifferentialSaveJob.ApplyDelta(DestinationFile, _diffFile, SourceFile);
            return File.Exists(SourceFile) ? new FileInfo(SourceFile).Length : 0;
        }
    }
}