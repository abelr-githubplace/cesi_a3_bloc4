using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Views;
using SaveManager;
using State;
using EasyLog;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EasySave.lang;
using EasySave.GUI.Helpers;
using Config;
using BusinessSoftware;
using System.IO;
using System.Text.Json;

namespace EasySave.GUI.ViewModels
{
    public class Main : ViewModel
    {
        private State.StateManager _stateManager;
        private ConfigManager _appConfig;

        public ObservableCollection<SaveJob> SaveJobs { get; set; }
        
        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected != value)
                {
                    _isAllSelected = value;
                    OnPropertyChanged();
                    if (SaveJobs != null)
                    {
                        foreach (var job in SaveJobs)
                        {
                            job.IsSelected = _isAllSelected;
                        }
                    }
                }
            }
        }

        private readonly Dictionary<SaveJob, Save.Saver> _activeSavers = new Dictionary<SaveJob, Save.Saver>();
        // Registered before Saver construction so Stop() works even during file enumeration.
        private readonly Dictionary<SaveJob, CancellationTokenSource> _activeCts = new Dictionary<SaveJob, CancellationTokenSource>();

        private SaveJob? _selectedJob;
        public SaveJob? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); }
        }

        public ICommand AddJobCommand { get; }
        public ICommand EditJobCommand { get; }
        public ICommand DeleteJobCommand { get; }
        public ICommand OpenOptionsCommand { get; }
        public ICommand RunSelectedJobCommand { get; }
        public ICommand RunAllJobsCommand { get; }

        public ICommand PlayJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand RestoreJobCommand { get; }

        public Main()
        {
            _appConfig = ConfigManager.Get();
            _stateManager = _appConfig.State;

            ApplySettingsFromOptions();

            // Single, global subscription: when the watcher flips, walk every
            // active save and flip its UI state in lockstep. Saver-side, each
            // Saver subscribes independently to gate its own loop.
            BusinessSoftwareWatcher.Get(_appConfig).Subscribe(OnBusinessSoftwareChanged);

            SaveJobs = new ObservableCollection<SaveJob>();
            LoadJobs();

            AddJobCommand = new RelayCommand(o => AddJob());

            EditJobCommand = new RelayCommand(EditJob, o => SelectedJob != null);
            DeleteJobCommand = new RelayCommand(DeleteJob, o => SelectedJob != null);

            OpenOptionsCommand = new RelayCommand(o => OpenOptions());

            RunSelectedJobCommand = new RelayCommand(ExecuteRunSelectedJobs, CanExecuteRunSelectedJobs);

            RunAllJobsCommand = new RelayCommand(o => RunAllJobs(), o => SaveJobs.Any());

            PlayJobCommand = new RelayCommand(PlayJob, o => o is SaveJob);
            PauseJobCommand = new RelayCommand(PauseJob, o => o is SaveJob);
            StopJobCommand = new RelayCommand(StopJob, o => o is SaveJob);
            RestoreJobCommand = new RelayCommand(RestoreJob, o => o is SaveJob);
        }

        private void ExecuteRunSelectedJobs(object parameter)
        {
            var jobsToRun = SaveJobs.Where(job => job.IsSelected).ToList();

            foreach (var job in jobsToRun)
            {
                RunJob(job);
            }
        }

        private bool CanExecuteRunSelectedJobs(object parameter)
        {
            if (SaveJobs == null) return false;
            return SaveJobs.Any(job => job.IsSelected);
        }

        private void ApplySettingsFromOptions()
        {
            try
            {
                string configPath = Path.Combine(AppContext.BaseDirectory, "gui_config.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    List<string> ReadStringList(string property, string? fallbackCsv = null)
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

                        if (!string.IsNullOrEmpty(fallbackCsv)
                            && root.TryGetProperty(fallbackCsv, out var fallback)
                            && fallback.ValueKind == JsonValueKind.String)
                        {
                            return ParseCsv(fallback.GetString());
                        }

                        return new List<string>();
                    }

                    static List<string> ParseCsv(string? raw)
                    {
                        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
                        return raw.Split(',')
                                  .Select(e => e.Trim())
                                  .Where(e => !string.IsNullOrEmpty(e))
                                  .ToList();
                    }

                    static string NormalizePath(string raw)
                    {
                        if (Path.IsPathRooted(raw)) return raw;
                        return Path.Combine(AppContext.BaseDirectory, raw);
                    }

                    static string? ReadString(JsonElement root, string property)
                    {
                        if (root.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
                        {
                            return prop.GetString();
                        }
                        return null;
                    }

                    static int? ReadInt(JsonElement root, string property)
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
                        return null;
                    }

                    if (root.TryGetProperty("LogFormat", out var formatProp))
                    {
                        string formatStr = formatProp.GetString() ?? "JSON";
                        if (Enum.TryParse<EasyLog.LogFormat>(formatStr, true, out var logFormat))
                        {
                            _appConfig.SetLogFormat(logFormat);
                        }
                    }

                    if (root.TryGetProperty("LogOutput", out _))
                    {
                        var logOutput = ReadString(root, "LogOutput");
                        if (!string.IsNullOrWhiteSpace(logOutput))
                        {
                            _appConfig.ModifyLogOutput(NormalizePath(logOutput));
                        }
                    }

                    if (root.TryGetProperty("StateOutput", out _))
                    {
                        var stateOutput = ReadString(root, "StateOutput");
                        if (!string.IsNullOrWhiteSpace(stateOutput))
                        {
                            _appConfig.ModifyStateOutput(NormalizePath(stateOutput));
                        }
                    }

                    // The Options window writes BusinessSoftwares as a JSON array
                    // of full paths. ConfigManager wants bare process names (no
                    // ".exe", no directory), since Process.GetProcessesByName
                    // matches on that. We wipe the existing list each pass so a
                    // remove in Options propagates without leaving stale entries.
                    if (root.TryGetProperty("BusinessSoftwares", out _))
                    {
                        var softwarePaths = ReadStringList("BusinessSoftwares");
                        var names = new List<string>();
                        foreach (var path in softwarePaths)
                        {
                            if (string.IsNullOrWhiteSpace(path)) continue;
                            names.Add(Path.GetFileNameWithoutExtension(path));
                        }

                        _appConfig.RemoveBusinessSoftwares(_appConfig.GetBusinessSoftwares().ToList());
                        if (names.Count > 0) _appConfig.AddBusinessSoftwares(names);
                    }

                    if (root.TryGetProperty("EncryptionExtensions", out _) || root.TryGetProperty("ExtensionsToEncrypt", out _))
                    {
                        var encryptionExtensions = ReadStringList("EncryptionExtensions", "ExtensionsToEncrypt");
                        _appConfig.RemoveEncryptionExtensions(_appConfig.GetEncryptionExtensions().ToList());
                        if (encryptionExtensions.Count > 0) _appConfig.AddEncryptionExtensions(encryptionExtensions);
                    }

                    // Same flow as ExtensionsToEncrypt: parse a CSV string,
                    // normalize, replace the persisted list.
                    if (root.TryGetProperty("PriorityExtensions", out _))
                    {
                        var priorityExtensions = ReadStringList("PriorityExtensions");
                        _appConfig.RemovePriorityExtensions(_appConfig.GetPriorityExtensions().ToList());
                        if (priorityExtensions.Count > 0) _appConfig.AddPriorityExtensions(priorityExtensions);
                    }

                    // Large file threshold: prefer MB key, fallback to legacy KB.
                    if (root.TryGetProperty("LargeFileThresholdMB", out _))
                    {
                        int? mb = ReadInt(root, "LargeFileThresholdMB");
                        if (mb.HasValue)
                        {
                            int kb = Math.Max(0, mb.Value) * 1024;
                            _appConfig.SetLargeFileThresholdKB(kb);
                        }
                    }
                    else if (root.TryGetProperty("LargeFileLimit", out _))
                    {
                        int? kb = ReadInt(root, "LargeFileLimit");
                        if (kb.HasValue)
                        {
                            _appConfig.SetLargeFileThresholdKB(kb.Value);
                        }
                    }

                    if (root.TryGetProperty("MaxWorkersPerJob", out _))
                    {
                        int? workers = ReadInt(root, "MaxWorkersPerJob");
                        if (workers.HasValue)
                        {
                            _appConfig.SetMaxWorkersPerJob(workers.Value);
                        }
                    }

                    if (root.TryGetProperty("CryptoSoftPath", out _))
                    {
                        var cryptoPath = ReadString(root, "CryptoSoftPath");
                        if (!string.IsNullOrWhiteSpace(cryptoPath))
                        {
                            _appConfig.SetCryptoSoftPath(NormalizePath(cryptoPath));
                        }
                    }

                    // Centralized log settings. LogMode must be one of the
                    // enum values; ignore unrecognised strings so a typo in
                    // gui_config.json doesn't crash the app.
                    if (root.TryGetProperty("LogMode", out var modeProp))
                    {
                        string modeStr = modeProp.GetString() ?? "Local";
                        if (Enum.TryParse<EasyLog.LogMode>(modeStr, true, out var logMode))
                        {
                            string host = "localhost";
                            int port = 9000;
                            if (root.TryGetProperty("RemoteLogHost", out var hostProp))
                                host = hostProp.GetString() ?? "localhost";
                            if (root.TryGetProperty("RemoteLogPort", out var portProp))
                            {
                                if (portProp.ValueKind == JsonValueKind.Number && portProp.TryGetInt32(out int pNum))
                                {
                                    if (pNum > 0) port = pNum;
                                }
                                else if (portProp.ValueKind == JsonValueKind.String)
                                {
                                    string rawPort = portProp.GetString() ?? "9000";
                                    if (int.TryParse(rawPort, out int p) && p > 0) port = p;
                                }
                            }
                            _appConfig.SetLogMode(logMode);
                            _appConfig.SetRemoteLogEndpoint(host, port);
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void LoadJobs()
        {
            SaveJobs.Clear();
            var saves = _stateManager.GetSaves();
            foreach (var save in saves)
            {
                SaveJobs.Add(new SaveJob(save));
            }
        }

        private void AddJob()
        {
            var editorVM = new SaveEditor();
            var window = new SaveEditorWindow { DataContext = editorVM };

            if (window.ShowDialog() == true)
            {
                var newSaveInfo = new SaveInfo
                {
                    SaveId = Guid.NewGuid(),
                    SaveName = editorVM.Name,
                    SourcePath = editorVM.SourcePath,
                    DestinationPath = editorVM.TargetPath,
                    SaveType = editorVM.Type
                };

                _stateManager.Save(MakeInactiveState(newSaveInfo));
                var job = new SaveJob(newSaveInfo);
                if (!string.IsNullOrEmpty(editorVM.Type)) job.Type = editorVM.Type;
                SaveJobs.Add(job);
            }
        }

        private void EditJob(object parameter)
        {
            var jobToEdit = parameter as SaveJob;
            if (jobToEdit == null) return;

            var editorVM = new SaveEditor(jobToEdit);
            var window = new SaveEditorWindow { DataContext = editorVM };

            if (window.ShowDialog() == true)
            {
                jobToEdit.Name = editorVM.Name;
                jobToEdit.SourcePath = editorVM.SourcePath;
                jobToEdit.TargetPath = editorVM.TargetPath;
                if (!string.IsNullOrEmpty(editorVM.Type)) jobToEdit.Type = editorVM.Type;

                _stateManager.Save(MakeInactiveState(jobToEdit.Model));
            }
        }

        private void DeleteJob(object parameter)
        {
            var job = parameter as SaveJob;
            if (job != null)
            {
                _stateManager.Delete(job.Model.SaveId);
                SaveJobs.Remove(job);
            }
        }

        // Driven by the global BusinessSoftwareWatcher. Flip every currently
        // active save's UI state. We only touch jobs that are in a state the
        // watcher controls (Running ↔ BusinessSoftwareDetected) so user-driven
        // Break/Stopped states stay where the user put them.
        private void OnBusinessSoftwareChanged(bool running)
        {
            lock (_activeSavers)
            {
                foreach (var job in _activeSavers.Keys)
                {
                    if (running)
                    {
                        if (job.State == TranslationSource.Instance["Running"])
                            job.State = TranslationSource.Instance["BusinessSoftwareDetected"];
                    }
                    else
                    {
                        if (job.State == TranslationSource.Instance["BusinessSoftwareDetected"])
                            job.State = TranslationSource.Instance["Running"];
                    }
                }
            }
        }

        private static SaveState MakeInactiveState(SaveInfo info)
        {
            return new SaveState
            {
                Id = info.SaveId,
                Name = info.SaveName,
                SourcePath = info.SourcePath,
                DestinationPath = info.DestinationPath,
                LastActionTime = DateTime.Now,
                Status = Status.Inactive,
                ActiveStateInfo = null,
                SaveType = info.SaveType
            };
        }


        private void OpenOptions()
        {
            var optionsVM = new Options();
            optionsVM.Applied += ApplySettingsFromOptions;
            var window = new OptionsWindow { DataContext = optionsVM };
            try
            {
                window.ShowDialog();
            }
            finally
            {
                optionsVM.Applied -= ApplySettingsFromOptions;
            }

            ApplySettingsFromOptions();
        }

        private void PlayJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                if (job.State == TranslationSource.Instance["Break"])
                {
                    lock (_activeSavers)
                    {
                        if (_activeSavers.TryGetValue(job, out var saver))
                        {
                            saver.Resume();
                            // If the worker is still parked in the business-software
                            // wait loop, surface that — don't pretend it's running.
                            job.State = saver.IsWaitingForBusinessSoftware
                                ? TranslationSource.Instance["BusinessSoftwareDetected"]
                                : TranslationSource.Instance["Running"];
                        }
                    }
                }
                else
                {
                    RunJob(job);
                }
            }
        }

        private void PauseJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                lock (_activeSavers)
                {
                    if (_activeSavers.TryGetValue(job, out var saver))
                    {
                        saver.Pause();
                        job.State = TranslationSource.Instance["Break"];
                    }
                    else
                    {
                        // Job not running, try to update state anyway
                        job.State = TranslationSource.Instance["Break"];
                    }
                }
            }
        }

        private void StopJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                // Cancel the token immediately — works even if the Saver is
                // still being constructed (enumerating source files). When
                // Start() is eventually called on a cancelled token, every
                // worker exits before touching the first file.
                bool found = false;
                lock (_activeCts)
                {
                    if (_activeCts.TryGetValue(job, out var cts))
                    {
                        cts.Cancel();
                        found = true;
                    }
                }
                if (found)
                {
                    job.State = TranslationSource.Instance["Stopped"];
                    // Also wake gate/bsGate if Start() is already running.
                    lock (_activeSavers)
                    {
                        if (_activeSavers.TryGetValue(job, out var saver))
                            saver.Stop();
                    }
                }
            }
        }

        private async void RunJob(SaveJob? job)
        {
            if (job == null) return;
            if (job.State == TranslationSource.Instance["Running"]) return;

            job.State = TranslationSource.Instance["Running"];
            job.Progress = 0f;

            // Register the CTS BEFORE entering Task.Run so that a Stop() click
            // during file enumeration (Saver constructor) is not silently dropped.
            var cts = new CancellationTokenSource();
            lock (_activeCts) { _activeCts[job] = cts; }

            await Task.Run(() =>
            {
                var progress = new Progress.Progress();
                var updater = new GuiProgressBar(job, progress);

                var saveAction = job.Type switch
                {
                    "Differential" => SaveManager.Action.DifferentialSave,
                    "Delta"        => SaveManager.Action.DeltaSave,
                    _              => SaveManager.Action.CompleteSave
                };
                var saver = new Save.Saver(job.Model, saveAction, progress, _appConfig, cts);

                lock (_activeSavers)
                {
                    _activeSavers[job] = saver;
                    // If the watcher already says business software is running
                    // when this save is registered, reflect it in the UI now —
                    // the global callback only fires on transitions.
                    if (BusinessSoftwareWatcher.Get(_appConfig).IsAnyRunning
                        && job.State == TranslationSource.Instance["Running"])
                    {
                        job.State = TranslationSource.Instance["BusinessSoftwareDetected"];
                    }
                }

                saver.Start(false);

                lock (_activeSavers)
                {
                    _activeSavers.Remove(job);
                }
                lock (_activeCts)
                {
                    _activeCts.Remove(job);
                }

                if (saver.IsStopped)
                {
                    job.State = TranslationSource.Instance["Stopped"];
                }
                else if (job.State == TranslationSource.Instance["Running"])
                {
                    job.State = TranslationSource.Instance["Finish"];
                    job.Progress = 100f;
                }
            });
        }

        private void RunAllJobs()
        {
            foreach (var job in SaveJobs)
            {
                RunJob(job);
            }
        }

        private async void RestoreJob(object parameter)
        {
            if (parameter is not SaveJob job) return;
            if (job.State == TranslationSource.Instance["Running"]) return;

            job.State = TranslationSource.Instance["Running"];
            job.Progress = 0f;

            await Task.Run(() =>
            {
                try
                {
                    var progress = new Progress.Progress();
                    var updater = new GuiProgressBar(job, progress);

                    var restorer = new Restore.Restorer(job.Model, SaveManager.Action.Restore, progress, _appConfig);
                    restorer.Start();

                    job.State = TranslationSource.Instance["Finish"];
                    job.Progress = 100f;
                }
                catch (Exception ex)
                {
                    job.State = TranslationSource.Instance["Error"] ?? "Error";
                    job.Progress = 0f;
                    _appConfig.Logger.Log($"[RESTORE ERROR] {job.Name}: {ex.Message}");
                }
            });
        }
    }
}