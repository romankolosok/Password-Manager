using System.Globalization;
using System.Windows.Data;

namespace PasswordManager.App.Converters
{
    /// <summary>
    /// Converts password strength (0–128) and track width to fill width for the strength bar.
    /// Use with MultiBinding: Bind to PasswordStrength and to the track's ActualWidth; Parameter = "128" (max bits).
    /// </summary>
    public sealed class PasswordStrengthToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return 0.0;
            var strength = values[0] is double d ? d : 0.0;
            var trackWidth = values[1] is double w ? w : 0.0;
            double max = 128.0;
            if (parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var p))
                max = p;
            if (trackWidth <= 0 || max <= 0) return 0.0;
            var ratio = Math.Clamp(strength / max, 0, 1);
            return ratio * trackWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
