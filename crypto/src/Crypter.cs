using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace EasyCrypt
{
    // AES-256-CBC with a per-file random IV stored as the first 16 bytes of the
    // output. Streaming so a multi-GB file does not need to fit in RAM
    // (CryptoSoft's File.ReadAllBytes was the constraint to lift).
    //
    // On-disk layout: [16-byte IV][ciphertext...]
    // The encrypted file is written to a sibling temp path then atomically
    // renamed over the source — partial writes never leave a half-encrypted file.
    public static class Crypter
    {
        private const int IvSize = 16;
        private const int BufferSize = 81920;

        public static int EncryptFile(string filePath, string passphrase)
        {
            if (!File.Exists(filePath)) return -1;
            if (string.IsNullOrEmpty(passphrase)) return -2;

            var sw = Stopwatch.StartNew();
            try
            {
                var key = DeriveKey(passphrase);
                var iv = RandomNumberGenerator.GetBytes(IvSize);

                string tempPath = filePath + ".enc.tmp";
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                    using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                    {
                        output.Write(iv, 0, iv.Length);
                        using (var encryptor = aes.CreateEncryptor())
                        using (var crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                        {
                            input.CopyTo(crypto, BufferSize);
                        }
                    }
                }

                File.Move(tempPath, filePath, overwrite: true);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch (Exception)
            {
                sw.Stop();
                return -99;
            }
        }

        public static int DecryptFile(string filePath, string passphrase)
        {
            if (!File.Exists(filePath)) return -1;
            if (string.IsNullOrEmpty(passphrase)) return -2;

            var sw = Stopwatch.StartNew();
            try
            {
                var key = DeriveKey(passphrase);
                string tempPath = filePath + ".dec.tmp";

                using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                {
                    var iv = new byte[IvSize];
                    int read = input.Read(iv, 0, IvSize);
                    if (read != IvSize) return -3;

                    using (var aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                        using (var decryptor = aes.CreateDecryptor())
                        using (var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                        {
                            crypto.CopyTo(output, BufferSize);
                        }
                    }
                }

                File.Move(tempPath, filePath, overwrite: true);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch (Exception)
            {
                sw.Stop();
                return -99;
            }
        }

        public static bool ShouldEncrypt(string filePath, IEnumerable<string>? extensions)
        {
            if (extensions == null) return false;
            string ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return false;
            foreach (var raw in extensions)
            {
                string normalized = raw.Trim();
                if (string.IsNullOrEmpty(normalized)) continue;
                if (!normalized.StartsWith('.')) normalized = "." + normalized;
                if (string.Equals(normalized, ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Stretch the user's passphrase to a 32-byte AES-256 key.
        // Fixed salt is acceptable here because the IV is per-file random;
        // the goal is "deterministic key from passphrase", not password storage.
        private static byte[] DeriveKey(string passphrase)
        {
            byte[] salt = Encoding.UTF8.GetBytes("EasyCrypt-v1-salt");
            return Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32);
        }
    }
}
