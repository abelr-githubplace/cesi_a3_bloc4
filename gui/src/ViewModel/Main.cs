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
            _appConfig = ConfigManager.Get();
            _stateManager = _appConfig.State;

            ApplySettingsFromOptions();

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
                if (File.Exists("./gui_config.json"))
                {
                    string json = File.ReadAllText("./gui_config.json");
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("LogFormat", out var formatProp))
                    {
                        string formatStr = formatProp.GetString() ?? "JSON";
                        if (Enum.TryParse<EasyLog.LogFormat>(formatStr, true, out var logFormat))
                        {
                            _appConfig.SetLogFormat(logFormat);
                        }
                    }

                    // The Options window writes BusinessSoftwares as a JSON array
                    // of full paths. ConfigManager wants bare process names (no
                    // ".exe", no directory), since Process.GetProcessesByName
                    // matches on that. We wipe the existing list each pass so a
                    // remove in Options propagates without leaving stale entries.
                    if (root.TryGetProperty("BusinessSoftwares", out var swArray)
                        && swArray.ValueKind == JsonValueKind.Array)
                    {
                        var names = new List<string>();
                        foreach (var item in swArray.EnumerateArray())
                        {
                            var path = item.GetString();
                            if (string.IsNullOrWhiteSpace(path)) continue;
                            names.Add(Path.GetFileNameWithoutExtension(path));
                        }

                        _appConfig.RemoveBusinessSoftwares(_appConfig.GetBusinessSoftwares().ToList());
                        if (names.Count > 0) _appConfig.AddBusinessSoftwares(names);
                    }

                    if (root.TryGetProperty("ExtensionsToEncrypt", out var extProp))
                    {
                        string extensions = extProp.GetString() ?? "";
                        var extensionList = extensions.Split(',')
                                                      .Select(e => e.Trim())
                                                      .Where(e => !string.IsNullOrEmpty(e))
                                                      .ToList();

                        _appConfig.RemoveEncryptionExtensions(_appConfig.GetEncryptionExtensions().ToList());
                        if (extensionList.Count > 0) _appConfig.AddEncryptionExtensions(extensionList);
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
                    DestinationPath = editorVM.TargetPath
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
                ActiveStateInfo = null
            };
        }


        private void OpenOptions()
        {
            var optionsVM = new Options();
            var window = new OptionsWindow { DataContext = optionsVM };
            window.ShowDialog();

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
                }
            }
        }

        private void StopJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                lock (_activeSavers)
                {
                    if (_activeSavers.TryGetValue(job, out var saver))
                    {
                        saver.Stop();
                        job.State = TranslationSource.Instance["Stopped"];
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

            await Task.Run(() =>
            {
                var progress = new Progress.Progress();
                var updater = new GuiProgressBar(job, progress);

                var saveAction = job.Type == TranslationSource.Instance["Complete"]
                    ? SaveManager.Action.CompleteSave
                    : SaveManager.Action.DifferentialSave;
                var saver = new Save.Saver(job.Model, saveAction, progress, _appConfig);

                // Flip the UI state label when the Saver enters/exits its
                // business-software auto-pause. Don't override a user-driven
                // Break/Stopped state set from the buttons.
                System.Action onBsStart = () =>
                {
                    if (job.State == TranslationSource.Instance["Running"])
                        job.State = TranslationSource.Instance["BusinessSoftwareDetected"];
                };
                System.Action onBsEnd = () =>
                {
                    if (job.State == TranslationSource.Instance["BusinessSoftwareDetected"])
                        job.State = TranslationSource.Instance["Running"];
                };
                saver.BusinessSoftwarePauseStarted += onBsStart;
                saver.BusinessSoftwarePauseEnded += onBsEnd;

                lock (_activeSavers)
                {
                    _activeSavers[job] = saver;
                }

                saver.Start(false);

                saver.BusinessSoftwarePauseStarted -= onBsStart;
                saver.BusinessSoftwarePauseEnded -= onBsEnd;

                lock (_activeSavers)
                {
                    _activeSavers.Remove(job);
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
    }
}