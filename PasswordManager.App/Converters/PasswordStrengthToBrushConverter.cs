using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PasswordManager.App.Converters;

public static class PasswordStrengthToBrushConverter
{
    private static readonly IBrush[] Brushes =
    [
        new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // 0 Very Weak
        new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)), // 1 Weak
        new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)), // 2 Fair
        new SolidColorBrush(Color.FromRgb(0x84, 0xCC, 0x16)), // 3 Strong
        new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // 4 Very Strong
    ];

    public static readonly FuncValueConverter<int, IBrush> Instance =
        new(score => score is >= 0 and <= 4 ? Brushes[score] : Brushes[0]);
}
