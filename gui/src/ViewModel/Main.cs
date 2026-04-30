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

namespace EasySave.GUI.ViewModels
{
    public class Main : ViewModel
    {
        private StateManager.StateManager _stateManager;
        private Config _config;

        public ObservableCollection<SaveJob> SaveJobs { get; set; }

        private SaveJob _selectedJob;
        public SaveJob SelectedJob
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
            _config = new Config { Logger = logger, StateManager = _stateManager };

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
                    SaveId = (uint)(SaveJobs.Count + 1),
                    SaveName = editorVM.Name,
                    SourcePath = editorVM.SourcePath,
                    DestinationPath = editorVM.TargetPath
                };

                SaveJobs.Add(new SaveJob(newSaveInfo));
                // TODO: Appeler _stateManager pour sauvegarder la liste mise à jour
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
                RunJob(job);
            }
        }

        private void PauseJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                // TODO: Appeler la méthode dans la lib pour mettre le thread en pause
                job.State = TranslationSource.Instance["Break"];
            }
        }

        private void StopJob(object parameter)
        {
            if (parameter is SaveJob job)
            {
                // TODO: Appeler la méthode dans la lib pour annuler la sauvegarde en cours
                job.State = TranslationSource.Instance["Stopped"];
            }
        }

        private async void RunJob(SaveJob job)
        {
            if (job == null) return;

            if (job.State == TranslationSource.Instance["Running"]) return;

            job.State = TranslationSource.Instance["Running"];

            await Task.Run(() =>
            {
                var progress = new Saver.Progress();

                var command = new Command
                {
                    SaveAction = SaveManager.Action.Save,
                    Saves = new[] { job.Model },
                    SaveType = job.Type == TranslationSource.Instance["Complete"] ? SaveType.Complete : SaveType.Differential
                };

                bool success = SaveManager.SaveManager.Execute(command, new[] { progress }, _config);

                if (job.State == TranslationSource.Instance["Running"])
                {
                    job.State = success ? TranslationSource.Instance["Complete"] : TranslationSource.Instance["Error1"];
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