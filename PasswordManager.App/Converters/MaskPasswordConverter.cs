using Avalonia.Data.Converters;

namespace PasswordManager.App.Converters;

public static class MaskPasswordConverter
{
    public static readonly FuncValueConverter<string?, string> Instance =
        new(password => string.IsNullOrEmpty(password)
            ? string.Empty
            : new string('\u2022', Math.Min(password.Length, 20)));
}
