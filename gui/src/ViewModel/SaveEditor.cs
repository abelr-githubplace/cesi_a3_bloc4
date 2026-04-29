using System.Windows.Input;
using EasySave.GUI.ViewModels.Base;

namespace EasySave.GUI.ViewModels
{
    public class SaveEditor : ViewModel
    {
        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _sourcePath;
        public string SourcePath { get => _sourcePath; set { _sourcePath = value; OnPropertyChanged(); } }

        private string _targetPath;
        public string TargetPath { get => _targetPath; set { _targetPath = value; OnPropertyChanged(); } }

        private string _type = "Complète";
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }

        public SaveEditor(SaveJob existingJob = null)
        {
            if (existingJob != null)
            {
                Name = existingJob.Name;
                SourcePath = existingJob.SourcePath;
                TargetPath = existingJob.TargetPath;
                Type = existingJob.Type;
            }

            BrowseSourceCommand = new RelayCommand(o => BrowseFolder(true));
            BrowseTargetCommand = new RelayCommand(o => BrowseFolder(false));
        }

        private void BrowseFolder(bool isSource)
        {
        }
    }
}