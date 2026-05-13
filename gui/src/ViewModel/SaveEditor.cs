using System.Windows.Input;
using EasySave.GUI.ViewModels.Base;
using Microsoft.Win32;
using Sanitize;

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

            BrowseSourceCommand = new RelayCommand(o => BrowseSourceFile());
            BrowseTargetCommand = new RelayCommand(o => BrowseTargetFolder());
        }

        private void BrowseSourceFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Sélectionnez le fichier source à sauvegarder",
                Filter = "Tous les fichiers (*.*)|*.*" 
            };

            if (dialog.ShowDialog() == true)
            {
                SourcePath = PathSanitizer.Sanitize(dialog.FileName);
            }
        }

        private void BrowseTargetFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionnez le dossier de destination"
            };

            if (dialog.ShowDialog() == true)
            {
                TargetPath = PathSanitizer.Sanitize(dialog.FolderName);
            }
        }
    }
}