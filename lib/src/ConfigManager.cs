using System.Text.Json;
using System.Text.Json.Serialization;
using EasyLog;
using State;

namespace Config
{
    public record ConfigData
    {
        // Client-specific
        public required string Lang { get; init; }
        public required string LogOutput { get; init; }
        public required string StateOutput { get; init; }

        // General
        public required LogFormat LogFormat { get; init; }
        public required HashSet<string> BusinessSoftwares { get; init; }
        public required HashSet<string> EncryptionExtensions { get; init; }
        // V3: a file whose extension is in this set is treated as priority.
        // No non-priority file (in any job) may start until every priority
        // file across every running save has begun copying.
        public required HashSet<string> PriorityExtensions { get; init; }
        public string EncryptionKey { get; init; } = "EasySaveDefaultKey";
        public string CryptoSoftPath { get; init; } = "";

        // V3: only one file larger than this threshold (in KiB) may be in
        // flight across the whole app at any time. Set to 0 or negative to
        // disable the gate entirely. Default 512 MiB — small enough that
        // multi-GB transfers don't saturate the network, large enough that
        // typical "big" files (~100 MiB game assets, photo libraries) still
        // run in parallel.
        public int LargeFileThresholdKB { get; init; } = 524288;

        public static ConfigData DefaultConfig()
        {
            return new()
            {
                Lang = "en-US",
                LogOutput = "./save.log",
                StateOutput = "./state.json",

                LogFormat = LogFormat.JSON,
                BusinessSoftwares = [],
                EncryptionExtensions = [],
                PriorityExtensions = [],
                EncryptionKey = "EasySaveDefaultKey",
                CryptoSoftPath = "",
                LargeFileThresholdKB = 524288,
            };
        }
    }
    

    public sealed class ConfigManager
    {
        private const string _output = "./config.json";
        private readonly static JsonSerializerOptions s_read_serializer = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        private readonly static JsonSerializerOptions s_write_serializer = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        private static ConfigManager? s_instance;
        private static readonly object s_lock = new();

        public Logger Logger { get; init; }
        public StateManager State { get; init; }

        public ConfigData Config { get; private set; }
        private static readonly object s_rwLock = new();

        private ConfigManager()
        {
            Config = ConfigData.DefaultConfig();
            Logger = Logger.Get(Config.LogOutput);
            State = StateManager.Get(Config.StateOutput);

            if (File.Exists(_output))
            {
                string json = File.ReadAllText(_output);
                if (string.IsNullOrWhiteSpace(json)) Config = ConfigData.DefaultConfig();
                else Config = JsonSerializer.Deserialize<ConfigData>(json, s_read_serializer)
                    ?? ConfigData.DefaultConfig();
                return;
            }
            Write();
        }

        public static ConfigManager Get()
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    s_instance ??= new ConfigManager();
                }
            }
            return s_instance;
        }

        public void SetLanguage(string lang)
        {
            lock(s_rwLock) { Config = Config with { Lang = lang }; }
            Write();
        }

        public void SetLogFormat(LogFormat format)
        {
            lock(s_rwLock) { Config = Config with { LogFormat = format }; }
            Write();
        }

        public LogFormat GetLogFormatConfig()
        {
            lock (s_rwLock) { return Config.LogFormat; }
        }

        public IReadOnlyList<string> GetBusinessSoftwares()
        {
            lock(s_rwLock) { return [..Config.BusinessSoftwares]; }
        }

        public void AddBusinessSoftwares(IEnumerable<string> names)
        {
            List<string> softwares = NormalizeProcessNames(names);
            bool added = false;
            lock (s_rwLock) {
                foreach (var software in softwares) added |= Config.BusinessSoftwares.Add(software);
            }
            if (added) Write();
        }

        public void RemoveBusinessSoftwares(IEnumerable<string> names)
        {
            List<string> softwares = NormalizeProcessNames(names);
            bool removed = false;
            lock (s_rwLock) {
                foreach (var software in softwares) removed |= Config.BusinessSoftwares.Remove(software);
            }
            if (removed) Write();
        }

        private static List<string> NormalizeProcessNames(IEnumerable<string> names)
        {
            List<string> normalized = [];
            foreach (var name in names) {
                string trimmed = name.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[0..^4];
                normalized.Add(trimmed);
            }
            return normalized;
        }

        public IReadOnlyList<string> GetEncryptionExtensions()
        {
            lock (s_rwLock) { return [..Config.EncryptionExtensions]; }
        }

        public void AddEncryptionExtensions(IEnumerable<string> extensions)
        {
            List<string> normed_extensions = NormalizeExtensions(extensions);
            bool added = false;
            foreach (var extension in normed_extensions)
            {
                lock(s_rwLock) { added |= Config.EncryptionExtensions.Add(extension); }
            }
            if (added) Write();
        }

        public void RemoveEncryptionExtensions(IEnumerable<string> extensions)
        {
            List<string> normed_extensions = NormalizeExtensions(extensions);
            bool removed = false;
            foreach (var extension in normed_extensions)
            {
                lock(s_rwLock) { removed |= Config.EncryptionExtensions.Remove(extension); }
            }
            if (removed) Write();
        }

        private static List<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            var seen = new HashSet<string>();
            var normalized = new List<string>();
            foreach (var extension in extensions)
            {
                if (string.IsNullOrWhiteSpace(extension)) continue;
                var trimmed = extension.Trim().ToLowerInvariant();
                if (!trimmed.StartsWith('.')) trimmed = "." + trimmed;
                if (seen.Add(trimmed)) normalized.Add(trimmed);
            }
            return normalized;
        }

        public string GetEncryptionKey()
        {
            lock (s_rwLock) { return Config.EncryptionKey ?? "EasySaveDefaultKey"; }
        }

        public void SetEncryptionKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            lock (s_rwLock) { Config = Config with { EncryptionKey = key }; }
            Write();
        }

        public string GetCryptoSoftPath()
        {
            lock (s_rwLock) { return Config.CryptoSoftPath ?? ""; }
        }

        public void SetCryptoSoftPath(string path)
        {
            lock (s_rwLock) { Config = Config with { CryptoSoftPath = path ?? "" }; }
            Write();
        }

        public int GetLargeFileThresholdKB()
        {
            lock (s_rwLock) { return Config.LargeFileThresholdKB; }
        }

        public void SetLargeFileThresholdKB(int thresholdKB)
        {
            lock (s_rwLock) { Config = Config with { LargeFileThresholdKB = thresholdKB }; }
            Write();
        }

        public IReadOnlyList<string> GetPriorityExtensions()
        {
            lock (s_rwLock) { return [..Config.PriorityExtensions]; }
        }

        public void AddPriorityExtensions(IEnumerable<string> extensions)
        {
            List<string> normed = NormalizeExtensions(extensions);
            bool added = false;
            foreach (var ext in normed)
            {
                lock (s_rwLock) { added |= Config.PriorityExtensions.Add(ext); }
            }
            if (added) Write();
        }

        public void RemovePriorityExtensions(IEnumerable<string> extensions)
        {
            List<string> normed = NormalizeExtensions(extensions);
            bool removed = false;
            foreach (var ext in normed)
            {
                lock (s_rwLock) { removed |= Config.PriorityExtensions.Remove(ext); }
            }
            if (removed) Write();
        }

        public void ModifyLogOutput(string output)
        {
            lock(s_rwLock)
            {
                Config = Config with { LogOutput = output };
            }
            Logger.ModifyOutput(output);
            Write();
        }

        public void ModifyStateOutput(string output)
        {
            lock(s_rwLock)
            {
                Config = Config with { StateOutput = output };
            }
            State.ModifyOutput(output);
            Write();
        }

        private void Write()
        {
            lock(s_rwLock)
            {
                var json = JsonSerializer.Serialize(Config, s_write_serializer);
                File.WriteAllText(_output, json);
            }
        }
    }
}
