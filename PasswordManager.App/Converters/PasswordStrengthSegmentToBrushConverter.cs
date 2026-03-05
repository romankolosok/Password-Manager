using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PasswordManager.App.Converters
{
    public sealed class PasswordStrengthSegmentToBrushConverter : IValueConverter
    {
        private static readonly IBrush Gray = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));

        private static readonly IBrush[] SegmentBrushes =
        [
            new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // segment 1 - red
            new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)), // segment 2 - orange
            new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)), // segment 3 - yellow
            new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // segment 4 - green
        ];

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int strength || strength < 0) return Gray;
            if (parameter is not string s || !int.TryParse(s, NumberStyles.None, culture, out int segment) || segment < 1 || segment > 4)
                return Gray;
            return strength >= segment ? SegmentBrushes[segment - 1] : Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
