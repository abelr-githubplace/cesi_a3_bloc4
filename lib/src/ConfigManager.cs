using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConfigManager
{
    public enum LogFormat { Json, Xml }

    public record AppConfig
    {
        public string Language { get; init; } = "en-US";
        public LogFormat LogFormat { get; init; } = LogFormat.Json;
        public List<string> EncryptionExtensions { get; init; } = new List<string>();
        public string BusinessSoftware { get; init; } = "";
        public string LogDirectory { get; init; } = "./logs";
        public string StateFilePath { get; init; } = "./state.json";
    }

    public sealed class ConfigManager
    {
        private static ConfigManager? s_instance;
        private static readonly object s_lock = new object();

        private AppConfig _config;
        private readonly string _outputFile;

        private ConfigManager(string outputFile)
        {
            _outputFile = outputFile;

            if (File.Exists(outputFile))
            {
                string json = File.ReadAllText(outputFile);
                if (string.IsNullOrWhiteSpace(json)) _config = new AppConfig();
                else _config = JsonSerializer.Deserialize<AppConfig>(
                    json,
                    new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }
                ) ?? new AppConfig();
            }
            else
            {
                _config = new AppConfig();
                Write();
            }
        }

        public static ConfigManager Get(string outputFile)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null) s_instance = new ConfigManager(outputFile);
                }
            }
            return s_instance;
        }

        public AppConfig GetConfig() => _config;

        public void SetLanguage(string language)
        {
            _config = _config with { Language = language };
            Write();
        }

        public void SetLogFormat(LogFormat format)
        {
            _config = _config with { LogFormat = format };
            Write();
        }

        public void SetEncryptionExtensions(IEnumerable<string> extensions)
        {
            _config = _config with { EncryptionExtensions = NormalizeExtensions(extensions) };
            Write();
        }

        public void AddEncryptionExtension(string extension)
        {
            var normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized)) return;
            if (_config.EncryptionExtensions.Contains(normalized)) return;
            var list = new List<string>(_config.EncryptionExtensions) { normalized };
            _config = _config with { EncryptionExtensions = list };
            Write();
        }

        public void RemoveEncryptionExtension(string extension)
        {
            var normalized = NormalizeExtension(extension);
            if (!_config.EncryptionExtensions.Contains(normalized)) return;
            var list = new List<string>(_config.EncryptionExtensions);
            list.Remove(normalized);
            _config = _config with { EncryptionExtensions = list };
            Write();
        }

        public void SetBusinessSoftware(string name)
        {
            _config = _config with { BusinessSoftware = name?.Trim() ?? "" };
            Write();
        }

        public void SetLogDirectory(string path)
        {
            _config = _config with { LogDirectory = path };
            Write();
        }

        public void SetStateFilePath(string path)
        {
            _config = _config with { StateFilePath = path };
            Write();
        }

        public void Update(AppConfig config)
        {
            _config = config with
            {
                EncryptionExtensions = NormalizeExtensions(config.EncryptionExtensions),
                BusinessSoftware = config.BusinessSoftware?.Trim() ?? ""
            };
            Write();
        }

        private static List<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var ext in extensions)
            {
                var n = NormalizeExtension(ext);
                if (string.IsNullOrEmpty(n)) continue;
                if (seen.Add(n)) result.Add(n);
            }
            return result;
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return "";
            var trimmed = extension.Trim().ToLowerInvariant();
            if (!trimmed.StartsWith(".")) trimmed = "." + trimmed;
            return trimmed;
        }

        private void Write()
        {
            var json = JsonSerializer.Serialize(
                _config,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new JsonStringEnumConverter() }
                }
            );
            File.WriteAllText(_outputFile, json);
        }
    }
}
