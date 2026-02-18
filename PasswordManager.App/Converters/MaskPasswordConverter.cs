using System.Globalization;
using System.Windows.Data;

namespace PasswordManager.App.Converters
{
    /// <summary>
    /// Converts a password string to a same-length mask of bullet characters for display in vault list.
    /// </summary>
    public sealed class MaskPasswordConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return new string('\u2022', Math.Min(s.Length, 20)); // cap at 20 dots for very long passwords
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
