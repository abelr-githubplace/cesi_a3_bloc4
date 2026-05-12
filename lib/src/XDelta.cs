using System.Data.SqlTypes;
using System.Security.Cryptography;

namespace XDelta
{
    public class XDelta
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
        private const long Padding = Mod * (long)(BlockSize + 1);

        public static string ComputeSha256(byte[] bytes)
        {
            byte[] hashBytes = SHA256.HashData(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
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

        private static uint RollAdler32(uint hash, byte outByte, byte inByte)
        {
            long a = ((hash & 0xFFFFu) - outByte + inByte + Mod) % Mod;
            long b = ((hash >> 16) - (long)BlockSize * outByte + a - 1 + Padding) % Mod;
            return ((uint)b << 16) | (uint)a;
        }

        private static void Flush(List<(byte Tag, int A, int B, byte[]? Data)> ops, List<byte> addBuf)
        {
            if (addBuf.Count > 0)
            {
                ops.Add((OpAdd, addBuf.Count, 0, addBuf.ToArray()));
                addBuf.Clear();
            }
        }

        private static byte[] Serialize(byte[] srcBytes, byte[] dstBytes, List<(byte Tag, int A, int B, byte[]? Data)> ops)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write(_diff);
                writer.Write(dstBytes.Length);
                writer.Write(srcBytes.Length);
                writer.Write(ops.Count);

                foreach ((byte tag, int a, int b, byte[]? data) in ops)
                {
                    writer.Write(tag);
                    switch (tag)
                    {
                        case OpCopy:
                            writer.Write(a); // srcOffset
                            writer.Write(b); // length
                            break;
                        case OpAdd:
                            writer.Write(a); // length
                            writer.Write(data!);
                            break;
                        default: throw new NotSupportedException("Operation is not possible");
                    }
                }

                return memoryStream.ToArray();
            }
        }

        public static byte[] Encode(byte[] srcBytes, byte[] dstBytes)
        {
            const int MaxCandidatesPerHash = 16;
            var index = new Dictionary<uint, List<int>>();
            for (int i = 0; i + BlockSize <= dstBytes.Length; i++)
            {
                uint h = Adler32(dstBytes, i, BlockSize);
                if (!index.TryGetValue(h, out var list)) { list = []; index[h] = list; }
                if (list.Count < MaxCandidatesPerHash) list.Add(i);
            }

            List<(byte Tag, int A, int B, byte[]? Data)> ops = [];
            List<byte> addBuf = [];

            int position = 0;
            bool haveHash = false;
            uint windowHash = 0;

            while (position < srcBytes.Length)
            {
                if (BlockSize > dstBytes.Length || position + BlockSize > srcBytes.Length)
                {
                    addBuf.Add(srcBytes[position]);
                    position++;
                    haveHash = false;
                    continue;
                }

                if (!haveHash)
                {
                    windowHash = Adler32(srcBytes, position, BlockSize);
                    haveHash = true;
                }

                int bestOff = -1;
                int bestFwd = 0;
                int bestBack = 0;
                if (index.TryGetValue(windowHash, out var candidates))
                {
                    foreach (int candOff in candidates)
                    {
                        // Forward
                        int fwd = 0;
                        while (candOff + fwd < dstBytes.Length && position + fwd < srcBytes.Length
                            && dstBytes[candOff + fwd] == srcBytes[position + fwd]) { fwd++; }
                        if (fwd < BlockSize) continue;

                        // Backward
                        int back = 0;
                        int maxBack = Math.Min(addBuf.Count, candOff);
                        while (back < maxBack && dstBytes[candOff - 1 - back] == srcBytes[position - 1 - back]) { back++; }

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
                    if (bestBack > 0) addBuf.RemoveRange(addBuf.Count - bestBack, bestBack);
                    Flush(ops, addBuf);
                    ops.Add((OpCopy, bestOff - bestBack, bestFwd + bestBack, null));
                    position += bestFwd;
                    haveHash = false;
                }
                else
                {
                    byte outByte = srcBytes[position];
                    addBuf.Add(outByte);
                    position++;
                    if (position + BlockSize <= srcBytes.Length)
                    {
                        byte inByte = srcBytes[position + BlockSize - 1];
                        windowHash = RollAdler32(windowHash, outByte, inByte);
                    }
                    else haveHash = false;
                }
            }
            Flush(ops, addBuf);
            return Serialize(srcBytes, dstBytes, ops);
        }

        public static byte[] Decode(byte[] dstBytes, byte[] diffBytes)
        {
            using MemoryStream ms = new(diffBytes);
            using BinaryReader reader = new(ms);

            int magic = reader.ReadInt32();
            if (magic != _diff) throw new InvalidDataException("Not a DIFF file");

            int oldLen = reader.ReadInt32();
            int newLen = reader.ReadInt32();
            if (oldLen != dstBytes.Length) throw new InvalidDataException("Old file size does not match diff header");

            int opCount = reader.ReadInt32();
            var result = new byte[newLen];
            int pos = 0;
            for (int i = 0; i < opCount; i++)
            {
                byte tag = reader.ReadByte();
                int length = 0;
                switch (tag)
                {
                    case OpCopy:
                        int srcOffset = reader.ReadInt32();
                        length = reader.ReadInt32();
                        if (srcOffset < 0 || length < 0 || srcOffset + length > dstBytes.Length)
                            throw new InvalidDataException("Copy op out of bounds");
                        if (pos + length > newLen)
                            throw new InvalidDataException("Copy op overruns output");
                        Array.Copy(dstBytes, srcOffset, result, pos, length);
                        pos += length;
                        break;
                    case OpAdd:
                        length = reader.ReadInt32();
                        if (pos + length > newLen)
                            throw new InvalidDataException("Add op overruns output");
                        int read = reader.Read(result, pos, length);
                        if (read != length)
                            throw new InvalidDataException("Diff truncated mid-Add");
                        pos += length;
                        break;
                    default: throw new InvalidDataException("Unknown op tag");
                }
            }

            if (pos != newLen) throw new InvalidDataException("Ops did not reconstruct the full output");
            return result;
        }
    }
}