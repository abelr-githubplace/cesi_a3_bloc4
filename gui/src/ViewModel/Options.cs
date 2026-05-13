using Config;
using EasySave.GUI.ViewModels.Base;
using Microsoft.Win32;
using Sanitize;
using System.Windows.Input;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        public ICommand SetLanguage_ { get; }
        public ICommand SetLogFormat_ { get; }
        public ICommand SetLogOutput_ { get; }
        public ICommand SetStateOutput_ { get; }
        public ICommand BrowseBusinessSoftwares_ { get; }
        public ICommand BrowseEncryptionExtensions_ { get; }

        public Options()
        {
            SetLanguage_ = new RelayCommand(SetLanguage);
            SetLogFormat_ = new RelayCommand(SetLogFormat);
            SetLogOutput_ = new RelayCommand(SetLogOutput);
            SetStateOutput_ = new RelayCommand(SetStateOutput);
            BrowseBusinessSoftwares_ = new RelayCommand(o => BrowseBusinessSoftwares());
            BrowseEncryptionExtensions_ = new RelayCommand(BrowseEncryptionExtensions);
        }

        private static void SetLanguage(object? parameter)
        {
            if (parameter is string lang) ConfigManager.Get().SetLanguage(lang);
        }
        
        private static void SetLogFormat(object? parameter)
        {
            if (parameter is EasyLog.LogFormat format) ConfigManager.Get().SetLogFormat(format);
        }

        private static void SetLogOutput(object? parameter)
        {
            if (parameter is string output)
            {
                var path = PathSanitizer.Sanitize(output);
                if (path == null) return;
                ConfigManager.Get().ModifyLogOutput(path);
            }
        }

        private static void SetStateOutput(object? parameter)
        {
            if (parameter is string output)
            {
                var path = PathSanitizer.Sanitize(output);
                if (path == null) return;
                ConfigManager.Get().ModifyStateOutput(path);
            }
        }

        private static void BrowseBusinessSoftwares()
        {
            string[] softwares = [];
            var dialog = new OpenFileDialog
            {
                Title = "Sélectionnez l'exécutable du logiciel métier",
                Filter = "Logiciels (*.exe)|*.exe|Tous les fichiers (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true) { softwares = [dialog.FileName]; }
            ConfigManager.Get().AddBusinessSoftwares(softwares);
        }
   
        private static void BrowseEncryptionExtensions(object? parameter)
        {
            if (parameter is IEnumerable<string> extensions) ConfigManager.Get().AddBusinessSoftwares(extensions);
        }
    }
}