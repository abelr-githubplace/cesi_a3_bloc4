using System.Collections.ObjectModel;
using System.Windows.Input;
using Config;
using SaveManager;
using EasySave.GUI.Views;
using EasySave.GUI.Helpers;
using EasySave.GUI.ViewModels.Base;

namespace EasySave.GUI.ViewModels
{
    public class Main : ViewModel
    {
        public ObservableCollection<SaveJob> SaveJobs { get; set; }
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

        public Main()
        {
            _selectedJob = null;
            SaveJobs = [];
            LoadJobs();

            AddJobCommand = new RelayCommand(o => AddJob());
            EditJobCommand = new RelayCommand(EditJob, o => SelectedJob != null);
            DeleteJobCommand = new RelayCommand(DeleteJob, o => SelectedJob != null);
            OpenOptionsCommand = new RelayCommand(o => OpenOptions());
            RunSelectedJobCommand = new RelayCommand(o => RunJob(SelectedJob), o => SelectedJob != null);
            RunAllJobsCommand = new RelayCommand(o => RunAllJobs(), o => SaveJobs.Any());
            PlayJobCommand = new RelayCommand(PlayJob, o => o is SaveJob);
        }
        private void LoadJobs()
        {
            SaveJobs.Clear();
            var saves = ConfigManager.Get().State.GetSaves();
            foreach (var save in saves) SaveJobs.Add(new SaveJob(save));
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

        private void EditJob(object? parameter)
        {
            if (parameter is SaveJob job)
            {
                var editorVM = new SaveEditor(job);
                var window = new SaveEditorWindow { DataContext = editorVM };
                if (window.ShowDialog() == true) { }
            }
        }

        private void DeleteJob(object? parameter)
        {
            if (parameter is SaveJob job) SaveJobs.Remove(job);
        }

        private static void OpenOptions()
        {
            var optionsVM = new Options();
            var window = new OptionsWindow { DataContext = optionsVM };
            window.ShowDialog();
        }

        private void PlayJob(object? parameter)
        {
            if (parameter is SaveJob job) { RunJob(job); }
        }

        private async static void RunJob(SaveJob? job)
        {
            if (job == null) return;
            if (job.State == TranslationSource.Instance["Running"]) return;
            job.State = TranslationSource.Instance["Running"];

            await Task.Run(() =>
            {
                Progress.Progress progress = new();
                Command command = new()
                {
                    SaveAction = job.Type == TranslationSource.Instance["Complete"] ? SaveManager.Action.CompleteSave : SaveManager.Action.DifferentialSave,
                    Saves = [job.Model],
                };
                var res = SaveManager.SaveManager.Execute(command, [progress]);
                if (res.IsErr) job.State = res.UnwrapErr().First();
            });
        }

        private void RunAllJobs()
        {
            foreach (var job in SaveJobs) RunJob(job);
        }
    }
}