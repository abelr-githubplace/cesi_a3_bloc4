using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        private readonly string _configFilePath = "./gui_config.json";

        private string _logFormat = "JSON";
        public string LogFormat
        {
            get => _logFormat;
            set { _logFormat = value; OnPropertyChanged(); SaveSettings(); }
        }

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

                SaveSettings();
            }
        }

        private string _businessSoftwareName = string.Empty;
        public string BusinessSoftwareName
        {
            get => _businessSoftwareName;
            set { _businessSoftwareName = value; OnPropertyChanged(); SaveSettings(); }
        }

        private string _extensionsToEncrypt = string.Empty;
        public string ExtensionsToEncrypt
        {
            get => _extensionsToEncrypt;
            set { _extensionsToEncrypt = value; OnPropertyChanged(); SaveSettings(); }
        }

        public ICommand BrowseSoftwareCommand { get; }

        public Options()
        {
            BrowseSoftwareCommand = new RelayCommand(o => BrowseSoftwareFile());
            LoadSettings();
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


        private class OptionsData
        {
            public string Language { get; set; } = "FR";
            public string LogFormat { get; set; } = "JSON";
            public string BusinessSoftwareName { get; set; } = string.Empty;
            public string ExtensionsToEncrypt { get; set; } = string.Empty;
        }

        private void SaveSettings()
        {
            try
            {
                var data = new OptionsData
                {
                    Language = this.language,
                    LogFormat = this.LogFormat,
                    BusinessSoftwareName = this.BusinessSoftwareName,
                    ExtensionsToEncrypt = this.ExtensionsToEncrypt
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception) {}
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var data = JsonSerializer.Deserialize<OptionsData>(json);

                    if (data != null)
                    {
                        _logFormat = data.LogFormat ?? "JSON";
                        _businessSoftwareName = data.BusinessSoftwareName ?? string.Empty;
                        _extensionsToEncrypt = data.ExtensionsToEncrypt ?? string.Empty;

                        OnPropertyChanged(nameof(LogFormat));
                        OnPropertyChanged(nameof(BusinessSoftwareName));
                        OnPropertyChanged(nameof(ExtensionsToEncrypt));

                        this.language = data.Language ?? "FR";
                    }
                }
            }
            catch (Exception) {}
        }
    }
}