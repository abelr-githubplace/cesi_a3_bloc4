using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EasySave.GUI.ViewModels
{
    public class OptionsViewModel : BaseViewModel
    {

        private bool _isJsonFormat = true;
        public bool IsJsonFormat { get => _isJsonFormat; set { _isJsonFormat = value; OnPropertyChanged(); } }

        private bool _isXmlFormat;
        public bool IsXmlFormat { get => _isXmlFormat; set { _isXmlFormat = value; OnPropertyChanged(); } }

        public ObservableCollection<string> BusinessSoftwares { get; set; } = new ObservableCollection<string>();
        public string NewBusinessSoftware { get; set; }
        public string SelectedBusinessSoftware { get; set; }

        public ObservableCollection<string> CryptoExtensions { get; set; } = new ObservableCollection<string>();
        public string NewCryptoExtension { get; set; }
        public string SelectedCryptoExtension { get; set; }

        public ICommand AddBusinessSoftwareCommand { get; }
        public ICommand RemoveBusinessSoftwareCommand { get; }
        public ICommand AddCryptoExtensionCommand { get; }
        public ICommand RemoveCryptoExtensionCommand { get; }
        public ICommand SaveOptionsCommand { get; }

        public OptionsViewModel()
        {
            BusinessSoftwares.Add("calculator.exe");
            CryptoExtensions.Add(".txt");
            CryptoExtensions.Add(".pdf");

            AddBusinessSoftwareCommand = new RelayCommand(o => {
                if (!string.IsNullOrWhiteSpace(NewBusinessSoftware) && !BusinessSoftwares.Contains(NewBusinessSoftware))
                {
                    BusinessSoftwares.Add(NewBusinessSoftware);
                    NewBusinessSoftware = string.Empty;
                    OnPropertyChanged(nameof(NewBusinessSoftware));
                }
            });

            RemoveBusinessSoftwareCommand = new RelayCommand(o => {
                if (SelectedBusinessSoftware != null) BusinessSoftwares.Remove(SelectedBusinessSoftware);
            });

            AddCryptoExtensionCommand = new RelayCommand(o => {
                if (!string.IsNullOrWhiteSpace(NewCryptoExtension) && !CryptoExtensions.Contains(NewCryptoExtension))
                {
                    CryptoExtensions.Add(NewCryptoExtension);
                    NewCryptoExtension = string.Empty;
                    OnPropertyChanged(nameof(NewCryptoExtension));
                }
            });

            RemoveCryptoExtensionCommand = new RelayCommand(o => {
                if (SelectedCryptoExtension != null) CryptoExtensions.Remove(SelectedCryptoExtension);
            });

            SaveOptionsCommand = new RelayCommand(o => SaveOptions());
        }

        private void SaveOptions()
        {
            System.Windows.MessageBox.Show("Paramètres enregistrés avec succès !");
        }
    }
}