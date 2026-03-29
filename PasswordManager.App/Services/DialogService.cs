using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PasswordManager.App.Views;

namespace PasswordManager.App.Services
{
    internal class DialogService : IDialogService
    {
        public async Task<bool> ConfirmAsync(string message, string title)
        {
            var parent = GetActiveWindow();
            if (parent == null)
                return false;

            var dialog = new ConfirmDialog(message, title);
            var result = await dialog.ShowDialog<bool?>(parent);
            return result == true;
        }

        private static Window? GetActiveWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow
                    ?? (desktop.Windows.Count > 0 ? desktop.Windows[0] : null);
            }
            return null;
        }
    }
}
