using System.Diagnostics;

namespace CryptoSoftRunner
{
    // Cahier des charges 2.0 : "le logiciel devra être capable de crypter
    // les fichiers en utilisant le logiciel CryptoSoft (réalisé durant
    // le prosit 4)". CryptoSoft is the external XOR utility distributed
    // as its own executable — EasySave never reimplements the cipher, it
    // shells out to the binary and propagates its exit code.
    //
    // Convention of the external binary (defined by CryptoSoft itself):
    //   <cryptosoft.exe> <filepath> <key>
    //   exit code >= 0  => elapsed milliseconds (success)
    //   exit code  < 0  => error code (propagated as-is into the log)
    //
    // CryptoSoft is symmetric (XOR-based), so EncryptFile and DecryptFile
    // both go through the same invocation. The public API mirrors the
    // shape of the previous in-tree Crypter so callers don't have to know
    // they're now hitting an out-of-process binary.
    public static class Crypter
    {
        public static int EncryptFile(string filePath, string passphrase, string? cryptoSoftPath = null)
            => Run(filePath, passphrase, cryptoSoftPath);

        public static int DecryptFile(string filePath, string passphrase, string? cryptoSoftPath = null)
            => Run(filePath, passphrase, cryptoSoftPath);

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

        private static int Run(string filePath, string passphrase, string? cryptoSoftPath)
        {
            if (!File.Exists(filePath)) return -1;
            if (string.IsNullOrEmpty(passphrase)) return -2;
            // CryptoSoft is now mandatory — no inline fallback. If the path
            // isn't configured or the binary isn't where we expect, surface
            // a distinct error code (-3) into the log rather than silently
            // shipping plaintext.
            if (string.IsNullOrWhiteSpace(cryptoSoftPath) || !File.Exists(cryptoSoftPath)) return -3;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cryptoSoftPath,
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
    }
}
