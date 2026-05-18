using Config;
using EasyLog;
using EasySave.GUI.Helpers;
using EasySave.GUI.ViewModels.Base;
using Microsoft.Win32;
using Sanitize;
using System.Collections.ObjectModel;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Windows.Input;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        // Properties
        private string _language = ConfigManager.Get().GetLanguageConfig();
        public string Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    ConfigManager.Get().SetLanguage(value);
                    TranslationSource.Instance.CurrentCulture = new CultureInfo(value);
                    OnPropertyChanged(nameof(Language));
                    OnPropertyChanged(string.Empty); // Notifies all properties
                }
            }
        }

        private string _logFormat = ConfigManager.Get().GetLogFormatConfig().ToString();
        public string LogFormat
        {
            get => _logFormat;
            set
            {
                if (_logFormat != value)
                {
                    switch (value)
                    {
                        case "JSON":
                            ConfigManager.Get().SetLogFormat(EasyLog.LogFormat.JSON);
                            break;
                        case "XML":
                            ConfigManager.Get().SetLogFormat(EasyLog.LogFormat.XML);
                            break;
                        case "Text":
                            ConfigManager.Get().SetLogFormat(EasyLog.LogFormat.Text);
                            break;
                    }
                    _logFormat = value;
                    OnPropertyChanged(nameof(LogFormat));
                }
            }
        }

        // ObservableCollections for dynamic UI updates
        public ObservableCollection<string> BusinessSoftwares { get; set; } = new(ConfigManager.Get().GetBusinessSoftwares());
        public ObservableCollection<string> EncryptionExtensions { get; set; } = new(ConfigManager.Get().GetEncryptionExtensions());

        // New extension/software fields
        private string _newEncryptionExtension = string.Empty;
        public string NewEncryptionExtension
        {
            get => _newEncryptionExtension;
            set
            {
                _newEncryptionExtension = value;
                OnPropertyChanged();
            }
        }

        private string _selectedBusinessSoftware = string.Empty;
        public string SelectedBusinessSoftware
        {
            get => _selectedBusinessSoftware;
            set
            {
                _selectedBusinessSoftware = value;
                OnPropertyChanged();
            }
        }

        public ICommand BrowseSoftwareCommand { get; }
        public ICommand RemoveSoftwareCommand { get; }
        public ICommand AddEncryptionExtensionCommand { get; }
        public ICommand RemoveEncryptionExtensionCommand { get; }

        public Options()
        {
            BrowseSoftwareCommand = new RelayCommand(_ => BrowseSoftware());
            RemoveSoftwareCommand = new RelayCommand(RemoveSoftware);
            AddEncryptionExtensionCommand = new RelayCommand(_ => AddEncryptionExtension());
            RemoveEncryptionExtensionCommand = new RelayCommand(RemoveEncryptionExtension);
        }

        private void BrowseSoftware()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Business Software Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string software = dialog.FileName;
                if (!BusinessSoftwares.Contains(software))
                {
                    BusinessSoftwares.Add(software);
                    ConfigManager.Get().AddBusinessSoftwares([software]);
                }
            }
        }

        private void RemoveSoftware(object? parameter)
        {
            if (parameter is string software)
            {
                BusinessSoftwares.Remove(software);
                ConfigManager.Get().RemoveBusinessSoftwares([software]);
            }
        }

        private void AddEncryptionExtension()
        {
            if (!string.IsNullOrWhiteSpace(NewEncryptionExtension) && !EncryptionExtensions.Contains(NewEncryptionExtension))
            {
                EncryptionExtensions.Add(NewEncryptionExtension);
                ConfigManager.Get().AddEncryptionExtensions([NewEncryptionExtension]);
                NewEncryptionExtension = string.Empty;
                OnPropertyChanged(nameof(NewEncryptionExtension));
            }
        }

        private void RemoveEncryptionExtension(object? parameter)
        {
            if (parameter is string extension)
            {
                EncryptionExtensions.Remove(extension);
                ConfigManager.Get().RemoveEncryptionExtensions([extension]);
            }
        }
    }
}