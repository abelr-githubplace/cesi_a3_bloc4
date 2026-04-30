using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        private string _logFormat = "TXT";
        public string LogFormat { get => _logFormat; set { _logFormat = value; OnPropertyChanged(); } }

        private string _language = "FR";
        public string language
        {
            get => _language;
            set
            {
                _language = value;
                OnPropertyChanged();

                if (value == "FR")
                    TranslationSource.Instance.CurrentCulture = new CultureInfo("fr-FR");
                else if (value == "EN")
                    TranslationSource.Instance.CurrentCulture = new CultureInfo("en-US");
            }
        }

        private string _businessSoftwareName = string.Empty;
        public string BusinessSoftwareName { get => _businessSoftwareName; set { _businessSoftwareName = value; OnPropertyChanged(); } }

        private string _extensionsToEncrypt = string.Empty;
        public string ExtensionsToEncrypt { get => _extensionsToEncrypt; set { _extensionsToEncrypt = value; OnPropertyChanged(); } }

        public ICommand BrowseSoftwareCommand { get; }

        public Options()
        {
            BrowseSoftwareCommand = new RelayCommand(o => BrowseSoftwareFile());

            // TODO: Charger les valeurs initiales depuis ton StateManager ici
        }

        private void BrowseSoftwareFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Sélectionnez l'exécutable du logiciel métier",
                Filter = "Logiciels (*.exe)|*.exe|Tous les fichiers (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                BusinessSoftwareName = dialog.FileName;
            }
        }
    }
}