using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace PasswordManager.App.Views
{
    public partial class UpdateNotificationDialog : Window
    {
        private readonly string _releaseUrl;

        public UpdateNotificationDialog()
        {
            InitializeComponent();
            _releaseUrl = string.Empty;
        }

        public UpdateNotificationDialog(string latestVersion, string releaseUrl) : this()
        {
            _releaseUrl = releaseUrl;
            VersionText.Text = $"Version {latestVersion} is now available.";
        }

        private void Download_Click(object? sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
            Close(true);
        }

        private void Later_Click(object? sender, RoutedEventArgs e) => Close(false);
    }
}
