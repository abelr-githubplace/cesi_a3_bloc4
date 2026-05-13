using System.ComponentModel;
using System.Globalization;
using EasySave.lang;

namespace EasySave.GUI.Helpers
{
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly TranslationSource s_instance = new();
        public static TranslationSource Instance => s_instance;
        public string this[string key] => Messages.ResourceManager.GetString(key, Messages.Culture) ?? key;
        public event PropertyChangedEventHandler? PropertyChanged;
        public CultureInfo CurrentCulture
        {
            get => Messages.Culture ?? CultureInfo.CurrentUICulture;
            set
            {
                if (!Equals(Messages.Culture, value))
                {
                    Messages.Culture = value;
                    Thread.CurrentThread.CurrentUICulture = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                }
            }
        }
    }
}