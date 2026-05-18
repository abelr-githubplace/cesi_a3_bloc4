using System.Windows.Input;
using Microsoft.Win32;
using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using Sanitize;

namespace EasySave.GUI.ViewModels
{
    public class SaveEditor : ViewModel
    {
        private string? _name;
        public string? Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string? _sourcePath;
        public string? SourcePath { get => _sourcePath; set { _sourcePath = value; OnPropertyChanged(); } }

        private string? _targetPath;
        public string? TargetPath { get => _targetPath; set { _targetPath = value; OnPropertyChanged(); } }

        private string _type = TranslationSource.Instance["Complete"];
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }

        public SaveEditor(SaveJob? existingJob = null)
        {
            if (existingJob != null)
            {
                Name = existingJob.Name;
                SourcePath = existingJob.SourcePath;
                TargetPath = existingJob.TargetPath;
                Type = existingJob.Type.ToString();
            }

            BrowseSourceCommand = new RelayCommand(o => BrowseSourceFile());
            BrowseTargetCommand = new RelayCommand(o => BrowseTargetFolder());
        }

        private void BrowseSourceFile()
        {
            var dialog = new OpenFolderDialog { Title = "Sélectionnez le dossier source à sauvegarder" };
            if (dialog.ShowDialog() == true) SourcePath = PathSanitizer.Sanitize(dialog.FolderName);
        }

        private void BrowseTargetFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Sélectionnez le dossier de destination" };
            if (dialog.ShowDialog() == true) TargetPath = PathSanitizer.Sanitize(dialog.FolderName);
        }
    }
}