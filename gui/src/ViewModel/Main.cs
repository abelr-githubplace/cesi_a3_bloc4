using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Views;
using SaveManager;
using StateManager;
using EasyLog;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EasySave.lang;
using EasySave.GUI.Helpers;
using Saver;

namespace EasySave.GUI.ViewModels
{
    public class Main : ViewModel
    {
        private StateManager.StateManager _stateManager;
        private Config _config;
        private AppConfig.AppConfig _appConfig;
        public ObservableCollection<SaveJob> SaveJobs { get; set; }

        // Per-job interrupt handles. The Saver lives on the worker thread; the
        // Pauser and Stopper are what the UI thread holds onto so the
        // Play/Pause/Stop buttons can emit signals without ever touching the
        // Saver directly.
        private readonly Dictionary<SaveJob, (Saver.Saver Saver, SaveInterrupt.Pauser Pauser, SaveInterrupt.Stopper Stopper)> _activeSavers
            = new Dictionary<SaveJob, (Saver.Saver, SaveInterrupt.Pauser, SaveInterrupt.Stopper)>();

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

        public Main()
        {
            var logger = Logger.Get(RuntimePaths.RuntimePaths.LogsDirectory);
            _stateManager = StateManager.StateManager.Get(RuntimePaths.RuntimePaths.StateFile);
            _appConfig = AppConfig.AppConfig.Get(RuntimePaths.RuntimePaths.ConfigFile);

            _config = new Config
            {
                Logger = logger,
                StateManager = _stateManager,
                LogFormat = ParseLogFormat(_appConfig.GetLogFormat()),
                AppConfig = _appConfig
            };

            ApplyCultureFromAppConfig();

            SaveJobs = new ObservableCollection<SaveJob>();
            LoadJobs();

            AddJobCommand = new RelayCommand(o => AddJob());

            EditJobCommand = new RelayCommand(EditJob, o => SelectedJob != null);
            DeleteJobCommand = new RelayCommand(DeleteJob, o => SelectedJob != null);

            OpenOptionsCommand = new RelayCommand(o => OpenOptions());

            // NewGUIcolors feature: multi-select via job.IsSelected checkboxes.
            // Made async + sequential per cahier des charges 2.0 (Mono ou Séquentielle).
            RunSelectedJobCommand = new RelayCommand(ExecuteRunSelectedJobs, CanExecuteRunSelectedJobs);
            RunAllJobsCommand = new RelayCommand(async o => await RunAllJobs(), o => SaveJobs.Any());

            PlayJobCommand = new RelayCommand(async o => await PlayJob(o), o => o is SaveJob);
            PauseJobCommand = new RelayCommand(PauseJob, o => o is SaveJob);
            StopJobCommand = new RelayCommand(StopJob, o => o is SaveJob);
        }

        // NewGUIcolors feature: run every job whose IsSelected checkbox is
        // ticked, sequentially. Async so we can await each RunJob — matches
        // the 2.0 cahier des charges "Mono ou Séquentielle".
        private async void ExecuteRunSelectedJobs(object parameter)
        {
            var jobsToRun = SaveJobs.Where(job => job.IsSelected).ToList();
            foreach (var job in jobsToRun)
            {
                await RunJob(job);
            }
        }

        private bool CanExecuteRunSelectedJobs(object parameter)
        {
            if (SaveJobs == null) return false;
            return SaveJobs.Any(job => job.IsSelected);
        }

        // Re-read whatever the Options window may have changed in AppConfig.
        // Only LogFormat lives in the SaveManager Config record — everything
        // else (business software, extensions, key, ...) is read on demand by
        // the lib through the AppConfig instance already attached to _config.
        private void RefreshConfigFromAppConfig()
        {
            _config = _config with { LogFormat = ParseLogFormat(_appConfig.GetLogFormat()) };
            ApplyCultureFromAppConfig();
        }

        private static EasyLog.LogFormat ParseLogFormat(string s)
        {
            return Enum.TryParse<EasyLog.LogFormat>(s, true, out var f) ? f : EasyLog.LogFormat.JSON;
        }

        private void ApplyCultureFromAppConfig()
        {
            string lang = _appConfig.GetLanguage();
            if (lang == "FR") TranslationSource.Instance.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            else if (lang == "EN") TranslationSource.Instance.CurrentCulture = new System.Globalization.CultureInfo("en-US");
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
                    DestinationPath = editorVM.TargetPath
                };

                var newJob = new SaveJob(newSaveInfo) { Type = editorVM.Type };
                SaveJobs.Add(newJob);

                // Persist to state.json so the new save survives a restart.
                _stateManager.Save(BuildInitialState(newSaveInfo));
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
                // The old StateManager entry is keyed on the previous Name —
                // remove it first so a rename doesn't leave a ghost entry.
                _stateManager.Delete(jobToEdit.Name);

                // Mutate the SaveJob in place so the DataGrid row refreshes
                // (each setter raises OnPropertyChanged).
                jobToEdit.Name = editorVM.Name;
                jobToEdit.SourcePath = editorVM.SourcePath;
                jobToEdit.TargetPath = editorVM.TargetPath;
                jobToEdit.Type = editorVM.Type;

                // SaveInfo is a record (immutable), so rebuild it with the
                // updated fields and replace the SaveJob's Model reference
                // via reflection of the public setter pattern: SaveJob
                // exposes GetUpdatedModel() for this purpose.
                var updated = jobToEdit.GetUpdatedModel();
                _stateManager.Save(BuildInitialState(updated));
            }
        }

        private void DeleteJob(object parameter)
        {
            var job = parameter as SaveJob;
            if (job != null)
            {
                // Persist the deletion so the entry doesn't reappear on next start.
                _stateManager.Delete(job.Name);
                SaveJobs.Remove(job);
            }
        }

        // Build a SaveState for a freshly-added or just-edited save: Inactive
        // status, no ActiveStateInfo, timestamp = now. The Saver will overwrite
        // this with live progress data as soon as it starts running.
        private SaveState BuildInitialState(SaveInfo info) => new SaveState
        {
            Id = info.SaveId,
            Name = info.SaveName,
            SourcePath = info.SourcePath,
            DestinationPath = info.DestinationPath,
            LastActionTime = DateTime.Now,
            Status = Status.Inactive,
            ActiveStateInfo = null,
        };

        private void OpenOptions()
        {
            var optionsVM = new Options();
            var window = new OptionsWindow { DataContext = optionsVM };
            window.ShowDialog();

            RefreshConfigFromAppConfig();
        }

        private async Task PlayJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                if (job.State == TranslationSource.Instance["Break"])
                {
                    lock (_activeSavers)
                    {
                        if (_activeSavers.TryGetValue(job, out var entry))
                        {
                            // Resume signal goes through the observer chain:
                            // pauser.Resume() -> Pauser.Notify()
                            // -> PauseListener.Update() -> _gate.Set() inside the Saver.
                            entry.Pauser.Resume();
                            job.State = TranslationSource.Instance["Running"];
                        }
                    }
                }
                else
                {
                    await RunJob(job);
                }
            }
        }

        private void PauseJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                lock (_activeSavers)
                {
                    if (_activeSavers.TryGetValue(job, out var entry))
                    {
                        entry.Pauser.Pause();
                        job.State = TranslationSource.Instance["Break"];
                    }
                }
            }
        }

        private void StopJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                lock (_activeSavers)
                {
                    if (_activeSavers.TryGetValue(job, out var entry))
                    {
                        entry.Stopper.Stop();
                        job.State = TranslationSource.Instance["Stopped"];
                    }
                }
            }
        }

        // Convenience: broadcast to every running save. Useful for a future
        // "Pause all" / "Stop all" toolbar — firing a Pauser or a Stopper is
        // just a Notify() so the cost is negligible.
        public void PauseAll()
        {
            lock (_activeSavers)
                foreach (var entry in _activeSavers.Values) entry.Pauser.Pause();
        }

        public void StopAll()
        {
            lock (_activeSavers)
                foreach (var entry in _activeSavers.Values) entry.Stopper.Stop();
        }

        private async Task RunJob(SaveJob? job)
        {
            if (job == null) return;
            if (job.State == TranslationSource.Instance["Running"]) return;

            // 3.0-style: no upfront block on business software. The Saver
            // polls between files and auto-pauses/resumes itself, so the
            // GUI launches every job regardless of detection state.
            job.State = TranslationSource.Instance["Running"];
            job.Progress = 0f;

            await Task.Run(() =>
            {
                var progress = new Saver.Progress();
                var updater = new GuiProgressBar(job, progress);
                var pauser = new SaveInterrupt.Pauser();
                var stopper = new SaveInterrupt.Stopper();

                var saveType = job.Type == TranslationSource.Instance["Complete"] ? SaveType.Complete : SaveType.Differential;
                // The Saver subscribes its two internal listeners to the
                // Pauser and Stopper in its constructor.
                var saver = new Saver.Saver(job.Model, saveType, progress, _config, pauser, stopper);

                // Surface the "auto-paused by business software" transition to
                // the user, so the State column shows a dedicated label instead
                // of staying on "Running" while the worker is actually waiting.
                // Distinct from the user-initiated pause label ("Break").
                saver.BusinessSoftwarePauseStarted += () =>
                {
                    job.State = TranslationSource.Instance["BusinessPaused"];
                };
                saver.BusinessSoftwarePauseEnded += () =>
                {
                    // Only flip back to Running if Stop wasn't pressed during the wait.
                    if (!saver.IsStopped)
                        job.State = TranslationSource.Instance["Running"];
                };

                lock (_activeSavers)
                {
                    _activeSavers[job] = (saver, pauser, stopper);
                }

                saver.Start();

                lock (_activeSavers)
                {
                    _activeSavers.Remove(job);
                }

                if (job.State == TranslationSource.Instance["Running"])
                {
                    job.State = saver.IsStopped ? TranslationSource.Instance["Stopped"] : TranslationSource.Instance["Finish"];

                    if (job.State == TranslationSource.Instance["Finish"])
                    {
                        job.Progress = 100f;
                    }
                }
            });
        }

        // Cahier des charges 2.0: type Mono ou Séquentielle (parallel is 3.0).
        // We await each job so the next one starts only once the previous is done.
        private async Task RunAllJobs()
        {
            foreach (var job in SaveJobs)
            {
                await RunJob(job);
            }
        }
    }
}