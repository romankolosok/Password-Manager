using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PasswordManager.App.Converters
{
    /// <summary>
    /// Converts Zxcvbn password strength score (0-4) to the matching gradient color for the strength label.
    /// </summary>
    public sealed class PasswordStrengthToBrushConverter : IValueConverter
    {
        private static readonly Brush[] Brushes = CreateBrushes();

        private static Brush[] CreateBrushes()
        {
            var b = new[]
            {
                new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // 0 Very Weak - red
                new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)), // 1 Weak - orange
                new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)), // 2 Fair - yellow
                new SolidColorBrush(Color.FromRgb(0x84, 0xCC, 0x16)), // 3 Strong - lime
                new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // 4 Very Strong - green
            };
            foreach (var brush in b) brush.Freeze();
            return b;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int score || score < 0 || score > 4)
                return Brushes[0];
            return Brushes[score];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
