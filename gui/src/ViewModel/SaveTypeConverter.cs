using System.Globalization;
using System.Windows.Data;

namespace EasySave.GUI.Helpers
{
    // Converts a canonical save-type string ("Complete" / "Differential") to
    // the currently selected UI language via TranslationSource.
    public class SaveTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key && !string.IsNullOrEmpty(key))
            {
                try { return TranslationSource.Instance[key]; }
                catch { return key; }
            }
            return value ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
