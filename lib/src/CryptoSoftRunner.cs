using System.Diagnostics;

namespace CryptoSoftRunner
{
    // Cahier des charges 2.0 : "le logiciel devra être capable de crypter
    // les fichiers en utilisant le logiciel CryptoSoft". CryptoSoft is the
    // external XOR utility distributed as its own executable — EasySave
    // never reimplements the cipher, it shells out to the binary and
    // propagates its exit code.
    //
    // Convention of the external binary:
    //   <cryptosoft.exe> <filepath> <key>
    //   exit code >= 0  => elapsed milliseconds (success)
    //   exit code  < 0  => error code (propagated as-is into the log)
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

            // Resolve the CryptoSoft binary path. When the caller doesn't
            // configure one, look next to the EasySave executable for a
            // "CryptoSoft.exe" sibling.
            string? resolved = cryptoSoftPath;
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            {
                string baseDir = AppContext.BaseDirectory;
                string candidate = Path.Combine(baseDir, "CryptoSoft.exe");
                if (File.Exists(candidate)) resolved = candidate;
                else
                {
                    // Try alternate location: same folder as executable but with different name
                    var exeDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        candidate = Path.Combine(exeDir, "CryptoSoft.exe");
                        if (File.Exists(candidate)) resolved = candidate;
                        else return -3;
                    }
                    else return -3;
                }
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = resolved,
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Crypto error: {ex.Message}");
                return -97;
            }
        }
    }
}
