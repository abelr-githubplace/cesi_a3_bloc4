using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

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

        private string _language = "EN";
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

        // --- NOUVELLE COLLECTION POUR LES LOGICIELS METIERS ---
        private ObservableCollection<string> _businessSoftwares = new ObservableCollection<string>();
        public ObservableCollection<string> BusinessSoftwares
        {
            get => _businessSoftwares;
            set { _businessSoftwares = value; OnPropertyChanged(); SaveSettings(); }
        }

        private string _extensionsToEncrypt = string.Empty;
        public string ExtensionsToEncrypt
        {
            get => _extensionsToEncrypt;
            set { _extensionsToEncrypt = value; OnPropertyChanged(); SaveSettings(); }
        }

        // Ajout des propriétés manquantes du XAML
        private string _priorityExtensions = string.Empty;
        public string PriorityExtensions
        {
            get => _priorityExtensions;
            set { _priorityExtensions = value; OnPropertyChanged(); SaveSettings(); }
        }

        private string _largeFileLimit = string.Empty;
        public string LargeFileLimit
        {
            get => _largeFileLimit;
            set { _largeFileLimit = value; OnPropertyChanged(); SaveSettings(); }
        }

        public ICommand BrowseSoftwareCommand { get; }
        public ICommand RemoveSoftwareCommand { get; } // Nouvelle commande

        public Options()
        {
            BrowseSoftwareCommand = new RelayCommand(o => BrowseSoftwareFile());
            RemoveSoftwareCommand = new RelayCommand(RemoveSoftware); // Initialisation de la commande
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
                // Vérifie si le logiciel n'est pas déjà dans la liste
                if (!BusinessSoftwares.Contains(dialog.FileName))
                {
                    BusinessSoftwares.Add(dialog.FileName);
                    SaveSettings(); // Force la sauvegarde car l'ajout ne déclenche pas le 'set'
                }
            }
        }

        private void RemoveSoftware(object parameter)
        {
            if (parameter is string softwareToRemove && BusinessSoftwares.Contains(softwareToRemove))
            {
                BusinessSoftwares.Remove(softwareToRemove);
                SaveSettings(); // Force la sauvegarde
            }
        }

        private class OptionsData
        {
            public string Language { get; set; } = "FR";
            public string LogFormat { get; set; } = "JSON";
            public List<string> BusinessSoftwares { get; set; } = new List<string>();
            public string ExtensionsToEncrypt { get; set; } = string.Empty;
            public string PriorityExtensions { get; set; } = string.Empty;
            public string LargeFileLimit { get; set; } = string.Empty;
        }

        private void SaveSettings()
        {
            try
            {
                var data = new OptionsData
                {
                    Language = this.language,
                    LogFormat = this.LogFormat,
                    BusinessSoftwares = new List<string>(this.BusinessSoftwares),
                    ExtensionsToEncrypt = this.ExtensionsToEncrypt,
                    PriorityExtensions = this.PriorityExtensions,
                    LargeFileLimit = this.LargeFileLimit
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception) { }
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
                        _extensionsToEncrypt = data.ExtensionsToEncrypt ?? string.Empty;
                        _priorityExtensions = data.PriorityExtensions ?? string.Empty;
                        _largeFileLimit = data.LargeFileLimit ?? string.Empty;

                        // Chargement de la liste
                        if (data.BusinessSoftwares != null)
                        {
                            _businessSoftwares = new ObservableCollection<string>(data.BusinessSoftwares);
                        }
                        else
                        {
                            _businessSoftwares = new ObservableCollection<string>();
                        }

                        OnPropertyChanged(nameof(LogFormat));
                        OnPropertyChanged(nameof(ExtensionsToEncrypt));
                        OnPropertyChanged(nameof(PriorityExtensions));
                        OnPropertyChanged(nameof(LargeFileLimit));
                        OnPropertyChanged(nameof(BusinessSoftwares)); // Important pour rafraîchir l'UI

                        this.language = data.Language ?? "FR";
                    }
                }
            }
            catch (Exception) { }
        }
    }
}