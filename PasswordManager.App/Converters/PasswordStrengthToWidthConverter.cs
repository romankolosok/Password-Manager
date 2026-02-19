using System.Globalization;
using System.Windows.Data;

namespace PasswordManager.App.Converters
{
    /// <summary>
    /// Converts password strength (0–4 Zxcvbn score) and track width to fill width for the strength bar.
    /// Use with MultiBinding: Bind to PasswordStrength and to the track's ActualWidth; Parameter = "4" (max Zxcvbn score).
    /// </summary>
    public sealed class PasswordStrengthToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return 0.0;
            // Handle both int (Zxcvbn score 0-4) and double (legacy entropy)
            var strength = values[0] switch
            {
                int i => (double)i,
                double d => d,
                _ => 0.0
            };
            var trackWidth = values[1] is double w ? w : 0.0;
            double max = 4.0; // Zxcvbn score range is 0-4
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
