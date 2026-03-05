using System.Globalization;
using Avalonia.Data.Converters;

namespace PasswordManager.App.Converters
{
    public sealed class MaskPasswordConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return new string('\u2022', Math.Min(s.Length, 20));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
