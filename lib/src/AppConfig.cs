using System.Text.Json;

namespace AppConfig
{
    public record AppConfigData
    {
        public List<string> BusinessSoftware { get; init; } = new List<string>();
        public List<string> EncryptionExtensions { get; init; } = new List<string>();
        public string EncryptionKey { get; init; } = "EasySaveDefaultKey";
        // Path to an external CryptoSoft executable. If empty / missing, Crypter
        // falls back to the inline AES implementation.
        public string CryptoSoftPath { get; init; } = string.Empty;
    }

    public sealed class AppConfig
    {
        private static AppConfig? s_instance;
        private static readonly object s_lock = new object();

        private readonly string _outputFile;
        private readonly object _writeLock = new object();
        private AppConfigData _data;

        private AppConfig(string outputFile)
        {
            _outputFile = outputFile;

            if (File.Exists(outputFile))
            {
                string json = File.ReadAllText(outputFile);
                _data = string.IsNullOrWhiteSpace(json)
                    ? new AppConfigData()
                    : JsonSerializer.Deserialize<AppConfigData>(json) ?? new AppConfigData();
            }
            else
            {
                _data = new AppConfigData();
                Write();
            }
        }

        public static AppConfig Get(string outputFile)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null) s_instance = new AppConfig(outputFile);
                }
            }
            return s_instance;
        }

        public IReadOnlyList<string> GetBusinessSoftware()
        {
            lock (_writeLock) return _data.BusinessSoftware.ToList();
        }

        public void AddBusinessSoftware(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            string normalized = NormalizeProcessName(processName);
            lock (_writeLock)
            {
                if (_data.BusinessSoftware.Any(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase))) return;
                _data.BusinessSoftware.Add(normalized);
                Write();
            }
        }

        public void RemoveBusinessSoftware(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            string normalized = NormalizeProcessName(processName);
            lock (_writeLock)
            {
                int removed = _data.BusinessSoftware.RemoveAll(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
                if (removed > 0) Write();
            }
        }

        public IReadOnlyList<string> GetEncryptionExtensions()
        {
            lock (_writeLock) return _data.EncryptionExtensions.ToList();
        }

        public void SetEncryptionExtensions(IEnumerable<string> extensions)
        {
            var normalized = extensions
                .Select(NormalizeExtension)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            lock (_writeLock)
            {
                _data = _data with { EncryptionExtensions = normalized };
                Write();
            }
        }

        public string GetEncryptionKey()
        {
            lock (_writeLock) return _data.EncryptionKey;
        }

        public void SetEncryptionKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            lock (_writeLock)
            {
                _data = _data with { EncryptionKey = key };
                Write();
            }
        }

        public string GetCryptoSoftPath()
        {
            lock (_writeLock) return _data.CryptoSoftPath ?? string.Empty;
        }

        public void SetCryptoSoftPath(string path)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            lock (_writeLock)
            {
                _data = _data with { CryptoSoftPath = trimmed };
                Write();
            }
        }

        private static string NormalizeExtension(string ext)
        {
            string trimmed = ext.Trim();
            if (string.IsNullOrEmpty(trimmed)) return string.Empty;
            if (!trimmed.StartsWith('.')) trimmed = "." + trimmed;
            return trimmed.ToLowerInvariant();
        }

        // Strip the .exe suffix so the stored name matches what Process.GetProcessesByName expects.
        private static string NormalizeProcessName(string name)
        {
            string trimmed = name.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 4);
            return trimmed;
        }

        private void Write()
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_outputFile, json);
        }
    }
}
