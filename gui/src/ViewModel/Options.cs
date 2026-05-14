using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        private readonly string _configFilePath = Path.Combine(AppContext.BaseDirectory, "gui_config.json");

        private bool _isDirty;
        private bool _isLoading;
        private DispatcherTimer? _applyToastTimer;

        public event Action? Applied;

        public bool IsDirty
        {
            get => _isDirty;
            private set { _isDirty = value; OnPropertyChanged(); }
        }

        private string _applyToastText = string.Empty;
        public string ApplyToastText
        {
            get => _applyToastText;
            private set { _applyToastText = value; OnPropertyChanged(); }
        }

        private Visibility _applyToastVisibility = Visibility.Collapsed;
        public Visibility ApplyToastVisibility
        {
            get => _applyToastVisibility;
            private set { _applyToastVisibility = value; OnPropertyChanged(); }
        }

        private string _logFormat = "JSON";
        public string LogFormat
        {
            get => _logFormat;
            set
            {
                if (_logFormat == value) return;
                _logFormat = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _language = "EN";
        public string language
        {
            get => _language;
            set
            {
                if (_language == value) return;
                _language = value;
                OnPropertyChanged();

                ApplyLanguage(value);
                MarkDirty();
            }
        }

        private string _logOutput = Path.Combine(AppContext.BaseDirectory, "save.log");
        public string LogOutput
        {
            get => _logOutput;
            set
            {
                if (_logOutput == value) return;
                _logOutput = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _stateOutput = Path.Combine(AppContext.BaseDirectory, "state.json");
        public string StateOutput
        {
            get => _stateOutput;
            set
            {
                if (_stateOutput == value) return;
                _stateOutput = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        // --- NOUVELLE COLLECTION POUR LES LOGICIELS METIERS ---
        private ObservableCollection<string> _businessSoftwares = new ObservableCollection<string>();
        public ObservableCollection<string> BusinessSoftwares
        {
            get => _businessSoftwares;
            set
            {
                _businessSoftwares = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private ObservableCollection<string> _encryptionExtensions = new ObservableCollection<string>();
        public ObservableCollection<string> EncryptionExtensions
        {
            get => _encryptionExtensions;
            set
            {
                _encryptionExtensions = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private ObservableCollection<string> _priorityExtensions = new ObservableCollection<string>();
        public ObservableCollection<string> PriorityExtensions
        {
            get => _priorityExtensions;
            set
            {
                _priorityExtensions = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _newEncryptionExtension = string.Empty;
        public string NewEncryptionExtension
        {
            get => _newEncryptionExtension;
            set { _newEncryptionExtension = value; OnPropertyChanged(); }
        }

        private string _newPriorityExtension = string.Empty;
        public string NewPriorityExtension
        {
            get => _newPriorityExtension;
            set { _newPriorityExtension = value; OnPropertyChanged(); }
        }

        private int _largeFileThresholdMB = 512;
        public int LargeFileThresholdMB
        {
            get => _largeFileThresholdMB;
            set
            {
                if (_largeFileThresholdMB == value) return;
                _largeFileThresholdMB = Math.Max(0, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LargeFileThresholdDisplay));
                MarkDirty();
            }
        }

        private int _maxWorkersPerJob = 4;
        public int MaxWorkersPerJob
        {
            get => _maxWorkersPerJob;
            set
            {
                if (_maxWorkersPerJob == value) return;
                _maxWorkersPerJob = Math.Clamp(value, 1, 8);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ParallelWorkersDisplay));
                MarkDirty();
            }
        }

        public string LargeFileThresholdDisplay
        {
            get
            {
                string template = TranslationSource.Instance["LargeFileThresholdHint"]
                    ?? "Threshold: {0} MB (0=disable)";
                return string.Format(template, LargeFileThresholdMB);
            }
        }

        public string ParallelWorkersDisplay
        {
            get
            {
                string template = TranslationSource.Instance["ParallelWorkersHint"]
                    ?? "Parallel workers: {0}";
                return string.Format(template, MaxWorkersPerJob);
            }
        }

        // Remote log settings
        private string _logMode = "Local";
        public string LogMode
        {
            get => _logMode;
            set
            {
                if (_logMode == value) return;
                _logMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRemoteLoggingEnabled));
                OnPropertyChanged(nameof(RemoteLogHint));
                MarkDirty();
            }
        }

        private string _remoteLogHost = "localhost";
        public string RemoteLogHost
        {
            get => _remoteLogHost;
            set
            {
                if (_remoteLogHost == value) return;
                _remoteLogHost = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _remoteLogPort = "9000";
        public string RemoteLogPort
        {
            get => _remoteLogPort;
            set
            {
                if (_remoteLogPort == value) return;
                _remoteLogPort = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        private string _cryptoSoftPath = string.Empty;
        public string CryptoSoftPath
        {
            get => _cryptoSoftPath;
            set
            {
                if (_cryptoSoftPath == value) return;
                _cryptoSoftPath = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        public bool IsRemoteLoggingEnabled =>
            string.Equals(_logMode, "Remote", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_logMode, "Both", StringComparison.OrdinalIgnoreCase);

        public string RemoteLogHint
        {
            get
            {
                if (IsRemoteLoggingEnabled)
                    return TranslationSource.Instance["RemoteLogEnabled"] ?? "Remote logging enabled";
                return TranslationSource.Instance["RemoteLogDisabled"] ?? "Remote logging is OFF";
            }
        }

        public ICommand BrowseSoftwareCommand { get; }
        public ICommand RemoveSoftwareCommand { get; }
        public ICommand BrowseLogOutputCommand { get; }
        public ICommand BrowseStateOutputCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand OpenStateFolderCommand { get; }
        public ICommand BrowseCryptoSoftCommand { get; }
        public ICommand AddEncryptionExtensionCommand { get; }
        public ICommand RemoveEncryptionExtensionCommand { get; }
        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand ApplyCommand { get; }

        public Options()
        {
            BrowseSoftwareCommand = new RelayCommand(o => BrowseSoftwareFile());
            RemoveSoftwareCommand = new RelayCommand(RemoveSoftware);
            BrowseLogOutputCommand = new RelayCommand(o => BrowseLogOutput());
            BrowseStateOutputCommand = new RelayCommand(o => BrowseStateOutput());
            OpenLogFolderCommand = new RelayCommand(o => OpenFolderForFile(LogOutput));
            OpenStateFolderCommand = new RelayCommand(o => OpenFolderForFile(StateOutput));
            BrowseCryptoSoftCommand = new RelayCommand(o => BrowseCryptoSoft());
            AddEncryptionExtensionCommand = new RelayCommand(o => AddEncryptionExtension());
            RemoveEncryptionExtensionCommand = new RelayCommand(RemoveEncryptionExtension);
            AddPriorityExtensionCommand = new RelayCommand(o => AddPriorityExtension());
            RemovePriorityExtensionCommand = new RelayCommand(RemovePriorityExtension);
            ApplyCommand = new RelayCommand(o => ApplyChanges());
            LoadSettings();
        }

        private void BrowseSoftwareFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Sélectionnez l'exécutable du logiciel métier",
                Filter = "Logiciels (*.exe)|*.exe|Tous les fichiers (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                // Vérifie si le logiciel n'est pas déjà dans la liste
                if (!BusinessSoftwares.Contains(dialog.FileName))
                {
                    BusinessSoftwares.Add(dialog.FileName);
                    MarkDirty();
                }
            }
        }

        private void RemoveSoftware(object parameter)
        {
            if (parameter is string softwareToRemove && BusinessSoftwares.Contains(softwareToRemove))
            {
                BusinessSoftwares.Remove(softwareToRemove);
                MarkDirty();
            }
        }

        private void BrowseLogOutput()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Select log output file",
                Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
                FileName = Path.GetFileName(LogOutput)
            };

            if (dialog.ShowDialog() == true)
            {
                LogOutput = dialog.FileName;
            }
        }

        private void BrowseStateOutput()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Select state output file",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = Path.GetFileName(StateOutput)
            };

            if (dialog.ShowDialog() == true)
            {
                StateOutput = dialog.FileName;
            }
        }

        private void BrowseCryptoSoft()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select CryptoSoft.exe",
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                FileName = Path.GetFileName(CryptoSoftPath)
            };

            if (dialog.ShowDialog() == true)
            {
                CryptoSoftPath = dialog.FileName;
            }
        }

        private static string ResolvePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return AppContext.BaseDirectory;
            if (Path.IsPathRooted(raw)) return raw;
            return Path.Combine(AppContext.BaseDirectory, raw);
        }

        private void OpenFolderForFile(string filePath)
        {
            try
            {
                string resolved = ResolvePath(filePath);
                string? dir = Path.GetDirectoryName(resolved);
                if (string.IsNullOrWhiteSpace(dir)) return;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
            catch (Exception) { }
        }

        private void AddEncryptionExtension()
        {
            var normalized = NormalizeExtension(NewEncryptionExtension);
            if (normalized == null) return;
            if (!_encryptionExtensions.Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                _encryptionExtensions.Add(normalized);
                OnPropertyChanged(nameof(EncryptionExtensions));
                MarkDirty();
            }
            NewEncryptionExtension = string.Empty;
        }

        private void RemoveEncryptionExtension(object parameter)
        {
            if (parameter is string ext && _encryptionExtensions.Contains(ext))
            {
                _encryptionExtensions.Remove(ext);
                OnPropertyChanged(nameof(EncryptionExtensions));
                MarkDirty();
            }
        }

        private void AddPriorityExtension()
        {
            var normalized = NormalizeExtension(NewPriorityExtension);
            if (normalized == null) return;
            if (!_priorityExtensions.Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                _priorityExtensions.Add(normalized);
                OnPropertyChanged(nameof(PriorityExtensions));
                MarkDirty();
            }
            NewPriorityExtension = string.Empty;
        }

        private void RemovePriorityExtension(object parameter)
        {
            if (parameter is string ext && _priorityExtensions.Contains(ext))
            {
                _priorityExtensions.Remove(ext);
                OnPropertyChanged(nameof(PriorityExtensions));
                MarkDirty();
            }
        }

        private void ApplyChanges()
        {
            if (!IsDirty) return;
            SaveSettings();
            IsDirty = false;
            Applied?.Invoke();
            ShowApplyToast();
        }

        private void ShowApplyToast()
        {
            string message = TranslationSource.Instance["ApplySuccess"] ?? "Changes applied";
            ApplyToastText = message;
            ApplyToastVisibility = Visibility.Visible;

            _applyToastTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _applyToastTimer.Stop();
            _applyToastTimer.Tick -= ApplyToastTimerTick;
            _applyToastTimer.Tick += ApplyToastTimerTick;
            _applyToastTimer.Start();
        }

        private void ApplyToastTimerTick(object? sender, EventArgs e)
        {
            _applyToastTimer?.Stop();
            ApplyToastText = string.Empty;
            ApplyToastVisibility = Visibility.Collapsed;
        }

        private void MarkDirty()
        {
            if (_isLoading) return;
            if (!IsDirty) IsDirty = true;
        }

        private static string? NormalizeExtension(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().ToLowerInvariant();
            if (!trimmed.StartsWith('.')) trimmed = "." + trimmed;
            if (trimmed.Length <= 1) return null;
            return trimmed;
        }

        private void ApplyLanguage(string value)
        {
            if (value == "FR")
                TranslationSource.Instance.CurrentCulture = new CultureInfo("fr-FR");
            else if (value == "EN")
                TranslationSource.Instance.CurrentCulture = new CultureInfo("en-US");
            OnPropertyChanged(nameof(RemoteLogHint));
            OnPropertyChanged(nameof(ParallelWorkersDisplay));
            OnPropertyChanged(nameof(LargeFileThresholdDisplay));
        }

        private class OptionsData
        {
            public string Language { get; set; } = "FR";
            public string LogFormat { get; set; } = "JSON";
            public List<string> BusinessSoftwares { get; set; } = new List<string>();
            public List<string> EncryptionExtensions { get; set; } = new List<string>();
            public List<string> PriorityExtensions { get; set; } = new List<string>();
            public string LargeFileLimit { get; set; } = string.Empty;
            public int LargeFileThresholdMB { get; set; } = 512;
            public int MaxWorkersPerJob { get; set; } = 4;
            public string LogMode { get; set; } = "Local";
            public string RemoteLogHost { get; set; } = "localhost";
            public string RemoteLogPort { get; set; } = "9000";
            public string LogOutput { get; set; } = Path.Combine(AppContext.BaseDirectory, "save.log");
            public string StateOutput { get; set; } = Path.Combine(AppContext.BaseDirectory, "state.json");
            public string CryptoSoftPath { get; set; } = string.Empty;
        }

        private void SaveSettings()
        {
            try
            {
                var data = new OptionsData
                {
                    Language = this.language,
                    LogFormat = this.LogFormat,
                    BusinessSoftwares = new List<string>(this.BusinessSoftwares),
                    EncryptionExtensions = new List<string>(this.EncryptionExtensions),
                    PriorityExtensions = new List<string>(this.PriorityExtensions),
                    LargeFileThresholdMB = this.LargeFileThresholdMB,
                    LargeFileLimit = (this.LargeFileThresholdMB * 1024).ToString(),
                    MaxWorkersPerJob = this.MaxWorkersPerJob,
                    LogMode = this.LogMode,
                    RemoteLogHost = this.RemoteLogHost,
                    RemoteLogPort = this.RemoteLogPort,
                    LogOutput = this.LogOutput,
                    StateOutput = this.StateOutput,
                    CryptoSoftPath = this.CryptoSoftPath
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception) { }
        }

        private void LoadSettings()
        {
            try
            {
                _isLoading = true;
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    _logFormat = ReadString(root, "LogFormat", "JSON");
                    _language = ReadString(root, "Language", "FR");
                    _largeFileThresholdMB = Math.Max(0, ReadInt(root, "LargeFileThresholdMB", 512));
                    if (_largeFileThresholdMB <= 0)
                    {
                        int kbFallback = ReadInt(root, "LargeFileLimit", 0);
                        _largeFileThresholdMB = kbFallback > 0 ? Math.Max(0, kbFallback / 1024) : 0;
                    }
                    _maxWorkersPerJob = Math.Clamp(ReadInt(root, "MaxWorkersPerJob", 4), 1, 8);
                    _cryptoSoftPath = ReadString(root, "CryptoSoftPath", string.Empty);
                    _logMode = ReadString(root, "LogMode", "Local");
                    _remoteLogHost = ReadString(root, "RemoteLogHost", "localhost");
                    _remoteLogPort = ReadString(root, "RemoteLogPort", "9000");
                    _logOutput = ReadString(root, "LogOutput", _logOutput);
                    _stateOutput = ReadString(root, "StateOutput", _stateOutput);

                    _businessSoftwares = new ObservableCollection<string>(ReadStringList(root, "BusinessSoftwares"));
                    _encryptionExtensions = new ObservableCollection<string>(NormalizeExtensions(ReadStringList(root, "EncryptionExtensions", "ExtensionsToEncrypt")));
                    _priorityExtensions = new ObservableCollection<string>(NormalizeExtensions(ReadStringList(root, "PriorityExtensions")));

                    ApplyLanguage(_language);

                    OnPropertyChanged(nameof(LogFormat));
                    OnPropertyChanged(nameof(language));
                    OnPropertyChanged(nameof(LargeFileThresholdMB));
                    OnPropertyChanged(nameof(MaxWorkersPerJob));
                    OnPropertyChanged(nameof(CryptoSoftPath));
                    OnPropertyChanged(nameof(LogMode));
                    OnPropertyChanged(nameof(RemoteLogHost));
                    OnPropertyChanged(nameof(RemoteLogPort));
                    OnPropertyChanged(nameof(IsRemoteLoggingEnabled));
                    OnPropertyChanged(nameof(RemoteLogHint));
                    OnPropertyChanged(nameof(ParallelWorkersDisplay));
                    OnPropertyChanged(nameof(LargeFileThresholdDisplay));
                    OnPropertyChanged(nameof(LogOutput));
                    OnPropertyChanged(nameof(StateOutput));
                    OnPropertyChanged(nameof(BusinessSoftwares));
                    OnPropertyChanged(nameof(EncryptionExtensions));
                    OnPropertyChanged(nameof(PriorityExtensions));
                    IsDirty = false;
                }
            }
            catch (Exception) { }
            finally { _isLoading = false; }
        }

        private static string ReadString(JsonElement root, string property, string fallback)
        {
            if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return fallback;
        }

        private static int ReadInt(JsonElement root, string property, int fallback)
        {
            if (root.TryGetProperty(property, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int value))
                    return value;
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var raw = prop.GetString();
                    if (int.TryParse(raw, out int parsed)) return parsed;
                }
            }
            return fallback;
        }

        private static List<string> ReadStringList(JsonElement root, string property, string? fallbackCsvProperty = null)
        {
            if (root.TryGetProperty(property, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in prop.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) list.Add(value);
                    }
                    return list;
                }
                if (prop.ValueKind == JsonValueKind.String)
                {
                    return ParseCsv(prop.GetString());
                }
            }

            if (!string.IsNullOrEmpty(fallbackCsvProperty)
                && root.TryGetProperty(fallbackCsvProperty, out var fallback)
                && fallback.ValueKind == JsonValueKind.String)
            {
                return ParseCsv(fallback.GetString());
            }

            return new List<string>();
        }

        private static List<string> ParseCsv(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(',')
                      .Select(e => e.Trim())
                      .Where(e => !string.IsNullOrEmpty(e))
                      .ToList();
        }

        private static List<string> NormalizeExtensions(IEnumerable<string> items)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var item in items)
            {
                var normalized = NormalizeExtension(item);
                if (normalized == null) continue;
                if (set.Add(normalized)) list.Add(normalized);
            }
            return list;
        }
    }
}