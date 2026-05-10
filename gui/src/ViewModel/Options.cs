using EasySave.GUI.ViewModels.Base;
using EasySave.GUI.Helpers;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;

namespace EasySave.GUI.ViewModels
{
    public class Options : ViewModel
    {
        private readonly AppConfig.AppConfig _appConfig;

        // Cahier des charges 2.0: all user preferences live in the single
        // AppConfig (config.json). The previous gui_config.json sibling has
        // been removed — Options is now a thin view over AppConfig.
        public Options()
        {
            _appConfig = AppConfig.AppConfig.Get(RuntimePaths.RuntimePaths.ConfigFile);

            _logFormat = _appConfig.GetLogFormat();
            _language = _appConfig.GetLanguage();
            var watched = _appConfig.GetBusinessSoftware();
            _businessSoftwareName = watched.Count > 0 ? watched[0] : string.Empty;
            _extensionsToEncrypt = string.Join(", ", _appConfig.GetEncryptionExtensions());

            BrowseSoftwareCommand = new RelayCommand(o => BrowseSoftwareFile());

            ApplyCulture(_language);
        }

        private string _logFormat;
        public string LogFormat
        {
            get => _logFormat;
            set
            {
                if (_logFormat == value) return;
                _logFormat = value;
                OnPropertyChanged();
                _appConfig.SetLogFormat(value);
            }
        }

        private string _language;
        public string language
        {
            get => _language;
            set
            {
                if (_language == value) return;
                _language = value;
                OnPropertyChanged();
                ApplyCulture(value);
                _appConfig.SetLanguage(value);
            }
        }

        private string _businessSoftwareName;
        public string BusinessSoftwareName
        {
            get => _businessSoftwareName;
            set
            {
                if (_businessSoftwareName == value) return;
                _businessSoftwareName = value;
                OnPropertyChanged();
                PersistBusinessSoftware(value);
            }
        }

        private string _extensionsToEncrypt;
        public string ExtensionsToEncrypt
        {
            get => _extensionsToEncrypt;
            set
            {
                if (_extensionsToEncrypt == value) return;
                _extensionsToEncrypt = value;
                OnPropertyChanged();
                var list = (value ?? string.Empty).Split(',')
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrEmpty(e));
                _appConfig.SetEncryptionExtensions(list);
            }
        }

        public ICommand BrowseSoftwareCommand { get; }

        private void BrowseSoftwareFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select the business software executable",
                Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
                BusinessSoftwareName = dialog.FileName;
        }

        // The UI exposes a single slot today. AppConfig keeps a list under the
        // hood (so 3.0 can extend the UI), so we keep the list in sync by
        // replacing it on every edit.
        private void PersistBusinessSoftware(string value)
        {
            foreach (var p in _appConfig.GetBusinessSoftware().ToList())
                _appConfig.RemoveBusinessSoftware(p);
            if (!string.IsNullOrWhiteSpace(value))
                _appConfig.AddBusinessSoftware(System.IO.Path.GetFileNameWithoutExtension(value));
        }

        private static void ApplyCulture(string code)
        {
            if (code == "FR") TranslationSource.Instance.CurrentCulture = new CultureInfo("fr-FR");
            else if (code == "EN") TranslationSource.Instance.CurrentCulture = new CultureInfo("en-US");
        }
    }
}
