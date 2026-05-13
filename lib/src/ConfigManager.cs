using EasyLog;
using Sanitize;
using State;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        public static ConfigData DefaultConfig()
        {
            return new()
            {
                Lang = "en-US",
                LogOutput = PathSanitizer.Sanitize("./save.log") ?? throw new Exception("default log file cannot be resolved"),
                StateOutput = PathSanitizer.Sanitize("./state.json") ?? throw new Exception("default state file cannot be resolved"),

                LogFormat = LogFormat.JSON,
                BusinessSoftwares = [],
                EncryptionExtensions = [],
            };
        }
    }
    

    public sealed class ConfigManager
    {
        private readonly string _output = PathSanitizer.Sanitize("./config.json") ?? throw new Exception("configuration file cannot be resolved");
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

        private ConfigData _config;
        private static readonly object s_rwLock = new();

        private ConfigManager()
        {
            _config = ConfigData.DefaultConfig();
            Logger = Logger.Get(_config.LogOutput);
            State = StateManager.Get(_config.StateOutput);

            if (File.Exists(_output))
            {
                string json = File.ReadAllText(_output);
                if (string.IsNullOrWhiteSpace(json)) _config = ConfigData.DefaultConfig();
                else _config = JsonSerializer.Deserialize<ConfigData>(json, s_read_serializer)
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

        public string GetLanguageConfig()
        {
            lock(s_rwLock) { return _config.Lang; }
        }

        public string GetLogOutput()
        {
            lock(s_rwLock) { return _config.LogOutput; }
        }

        public string GetStateOutput()
        {
            lock(s_rwLock) { return _config.StateOutput; }
        }

        public void SetLanguage(string lang)
        {
            lock(s_rwLock) { _config = _config with { Lang = lang }; }
            Write();
        }

        public void SetLogFormat(LogFormat format)
        {
            lock(s_rwLock) { _config = _config with { LogFormat = format }; }
            Write();
        }

        public LogFormat GetLogFormatConfig()
        {
            lock (s_rwLock) { return _config.LogFormat; }
        }

        public IReadOnlyList<string> GetBusinessSoftwares()
        {
            lock(s_rwLock) { return [.._config.BusinessSoftwares]; }
        }

        public void AddBusinessSoftwares(IEnumerable<string> names)
        {
            List<string> softwares = NormalizeProcessNames(names);
            bool added = false;
            lock (s_rwLock) {
                foreach (var software in softwares) added |= _config.BusinessSoftwares.Add(software);
            }
            if (added) Write();
        }

        public void RemoveBusinessSoftwares(IEnumerable<string> names)
        {
            List<string> softwares = NormalizeProcessNames(names);
            bool removed = false;
            lock (s_rwLock) {
                foreach (var software in softwares) removed |= _config.BusinessSoftwares.Remove(software);
            }
            if (removed) Write();
        }

        private static List<string> NormalizeProcessNames(IEnumerable<string> names)
        {
            List<string> normalized = [];
            foreach (var name in names) {
                string trimmed = name.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[0..^4];
                normalized.Add(trimmed);
            }
            return normalized;
        }

        public IReadOnlyList<string> GetEncryptionExtensions()
        {
            lock (s_rwLock) { return [.._config.EncryptionExtensions]; }
        }

        public void AddEncryptionExtensions(IEnumerable<string> extensions)
        {
            List<string> normed_extensions = NormalizeExtensions(extensions);
            bool added = false;
            foreach (var extension in normed_extensions)
            {
                lock(s_rwLock) { added |= _config.EncryptionExtensions.Add(extension); }
            }
            if (added) Write();
        }

        public void RemoveEncryptionExtensions(IEnumerable<string> extensions)
        {
            List<string> normed_extensions = NormalizeExtensions(extensions);
            bool removed = false;
            foreach (var extension in normed_extensions)
            {
                lock(s_rwLock) { removed |= _config.EncryptionExtensions.Remove(extension); }
            }
            if (removed) Write();
        }

        private static List<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            var seen = new HashSet<string>();
            var normalized = new List<string>();
            foreach (var extension in extensions)
            {
                var trimmed = extension.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(extension)) continue;
                if (!trimmed.StartsWith('.')) trimmed = "." + trimmed;
                if (seen.Add(trimmed)) normalized.Add(trimmed);
            }
            return normalized;
        }

        public void ModifyLogOutput(string output)
        {
            lock(s_rwLock)
            {
                _config = _config with { LogOutput = output };
            }
            Logger.ModifyOutput(output);
            Write();
        }

        public void ModifyStateOutput(string output)
        {
            lock(s_rwLock)
            {
                _config = _config with { StateOutput = output };
            }
            State.ModifyOutput(output);
            Write();
        }

        private void Write()
        {
            lock(s_rwLock)
            {
                var json = JsonSerializer.Serialize(_config, s_write_serializer);
                File.WriteAllText(_output, json);
            }
        }
    }
}
