using EasySave.GUI.ViewModels.Base;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        private string _logFormat = "TXT";
        public string LogFormat { get => _logFormat; set { _logFormat = value; OnPropertyChanged(); } }

        private string _language = "FR";
        public string language { get => _language; set { _language = value; OnPropertyChanged(); } }

        private string _businessSoftwareName;
        public string BusinessSoftwareName { get => _businessSoftwareName; set { _businessSoftwareName = value; OnPropertyChanged(); } }

        private string _extensionsToEncrypt;
        public string ExtensionsToEncrypt { get => _extensionsToEncrypt; set { _extensionsToEncrypt = value; OnPropertyChanged(); } }

        public Options()
        {
            // TODO: Charger ces valeurs depuis un fichier de config ou le StateManager
        }

    }
}