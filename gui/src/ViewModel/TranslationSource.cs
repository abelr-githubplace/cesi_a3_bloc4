using System.ComponentModel;
using System.Globalization;
using EasySave.lang;

namespace EasySave.GUI.Helpers
{
    public class TranslationSource : INotifyPropertyChanged
    {
        private static readonly TranslationSource _instance = new TranslationSource();
        public static TranslationSource Instance => _instance;

        public string this[string key] => Messages.ResourceManager.GetString(key, Messages.Culture) ?? key;

        public CultureInfo CurrentCulture
        {
            get => Messages.Culture ?? CultureInfo.CurrentUICulture;
            set
            {
                if (!Equals(Messages.Culture, value))
                {
                    Messages.Culture = value;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}