using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PasswordManager.App.Views
{
    public partial class ConfirmDialog : Window
    {
        public ConfirmDialog()
        {
            InitializeComponent();
        }

        public ConfirmDialog(string message, string title) : this()
        {
            Title = title;
            MessageText.Text = message;
        }

        private void Yes_Click(object? sender, RoutedEventArgs e) => Close(true);
        private void No_Click(object? sender, RoutedEventArgs e) => Close(false);
    }
}
