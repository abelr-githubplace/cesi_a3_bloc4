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

        public ObservableCollection<SaveJob> SaveJobs { get; set; }

        private readonly Dictionary<SaveJob, Saver.Saver> _activeSavers = new Dictionary<SaveJob, Saver.Saver>();
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
            var logger = Logger.Get("./save.log");
            _stateManager = StateManager.StateManager.Get("./state.json");

            _config = new Config { Logger = logger, StateManager = _stateManager, LogFormat = EasyLog.LogFormat.JSON };
            SaveJobs = new ObservableCollection<SaveJob>();
            LoadJobs();

            AddJobCommand = new RelayCommand(o => AddJob());
            EditJobCommand = new RelayCommand(EditJob, o => SelectedJob != null);
            DeleteJobCommand = new RelayCommand(DeleteJob, o => SelectedJob != null);
            OpenOptionsCommand = new RelayCommand(o => OpenOptions());
            RunSelectedJobCommand = new RelayCommand(o => RunJob(SelectedJob), o => SelectedJob != null);
            RunAllJobsCommand = new RelayCommand(o => RunAllJobs(), o => SaveJobs.Any());

            PlayJobCommand = new RelayCommand(PlayJob, o => o is SaveJob);
            PauseJobCommand = new RelayCommand(PauseJob, o => o is SaveJob);
            StopJobCommand = new RelayCommand(StopJob, o => o is SaveJob);
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

                SaveJobs.Add(new SaveJob(newSaveInfo));
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
            }
        }

        private void DeleteJob(object parameter)
        {
            var job = parameter as SaveJob;
            if (job != null)
            {
                SaveJobs.Remove(job);
            }
        }

        private void OpenOptions()
        {
            var optionsVM = new Options();
            var window = new OptionsWindow { DataContext = optionsVM };
            window.ShowDialog();
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
                            job.State = TranslationSource.Instance["Running"];
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

            await Task.Run(() =>
            {
                var progress = new Saver.Progress();

                var saveType = job.Type == TranslationSource.Instance["Fisnish"] ? SaveType.Complete : SaveType.Differential;
                var saver = new Saver.Saver(job.Model, saveType, progress, _config);

                lock (_activeSavers)
                {
                    _activeSavers[job] = saver;
                }

                saver.Start();

                lock (_activeSavers)
                {
                    _activeSavers.Remove(job);
                }

                if (job.State == TranslationSource.Instance["Running"])
                {
                    job.State = saver.IsStopped ? TranslationSource.Instance["Stopped"] : TranslationSource.Instance["Finish"];
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