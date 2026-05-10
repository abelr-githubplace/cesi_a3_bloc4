using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace EasyCrypt
{
    // Cahier des charges 2.0 : "le logiciel devra être capable de crypter les
    // fichiers en utilisant le logiciel CryptoSoft (réalisé durant le prosit 4)".
    //
    // The public API still exposes EncryptFile/DecryptFile, but when an external
    // CryptoSoft binary path is provided, we shell out to it via Process.Start
    // instead of running AES inline. The inline AES implementation is kept as a
    // fallback so the app stays usable in environments where CryptoSoft is not
    // installed (CI, dev machines without the prosit 4 binary).
    //
    // Convention for the external binary:
    //   <cryptosoft.exe> <filepath> <key>
    //   exit code = elapsed milliseconds on success (>= 0)
    //   exit code < 0  = error code propagated as-is
    // CryptoSoft is symmetric (XOR-based in the prosit 4 spec), so encrypt and
    // decrypt go through the same invocation.
    //
    // On-disk layout for the inline fallback: [16-byte IV][AES-256-CBC ciphertext].
    public static class Crypter
    {
        private const int IvSize = 16;
        private const int BufferSize = 81920;

        public static int EncryptFile(string filePath, string passphrase, string? cryptoSoftPath = null)
        {
            if (!File.Exists(filePath)) return -1;
            if (string.IsNullOrEmpty(passphrase)) return -2;

            if (!string.IsNullOrWhiteSpace(cryptoSoftPath) && File.Exists(cryptoSoftPath))
                return RunCryptoSoft(cryptoSoftPath, filePath, passphrase);

            return EncryptFileInline(filePath, passphrase);
        }

        public static int DecryptFile(string filePath, string passphrase, string? cryptoSoftPath = null)
        {
            if (!File.Exists(filePath)) return -1;
            if (string.IsNullOrEmpty(passphrase)) return -2;

            if (!string.IsNullOrWhiteSpace(cryptoSoftPath) && File.Exists(cryptoSoftPath))
                return RunCryptoSoft(cryptoSoftPath, filePath, passphrase);

            return DecryptFileInline(filePath, passphrase);
        }

        // External CryptoSoft invocation. Stdout is captured for diagnostics but
        // the milliseconds / error code is read from the exit code per the
        // documented convention above.
        private static int RunCryptoSoft(string exePath, string filePath, string passphrase)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add(filePath);
                psi.ArgumentList.Add(passphrase);

                using var proc = Process.Start(psi);
                if (proc == null) return -98;
                proc.WaitForExit();
                return proc.ExitCode;
            }
            catch (Exception)
            {
                return -97;
            }
        }

        private static int EncryptFileInline(string filePath, string passphrase)
        {
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

        private static int DecryptFileInline(string filePath, string passphrase)
        {
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

        // Stretch the user's passphrase to a 32-byte AES-256 key (inline fallback).
        // Fixed salt is acceptable here because the IV is per-file random;
        // the goal is "deterministic key from passphrase", not password storage.
        private static byte[] DeriveKey(string passphrase)
        {
            byte[] salt = Encoding.UTF8.GetBytes("EasyCrypt-v1-salt");
            return Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32);
        }
    }
}
