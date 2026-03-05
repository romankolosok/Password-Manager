using Avalonia.Controls;
using Avalonia.Interactivity;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    public partial class LoginView : Window
    {
        private LoginViewModel? _subscribedVm;

        public IAuthCoordinator? Coordinator { get; set; }

        public LoginView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedVm != null)
                _subscribedVm.LoginSuccessful -= OnLoginSuccessful;

            _subscribedVm = DataContext as LoginViewModel;
            if (_subscribedVm != null)
                _subscribedVm.LoginSuccessful += OnLoginSuccessful;
        }

        private void OnLoginSuccessful(object? sender, System.EventArgs e)
        {
            Coordinator?.OnLoginSuccess(this);
        }

        private void RegisterButton_Click(object? sender, RoutedEventArgs e)
        {
            Coordinator?.RequestRegister(this);
        }
    }
}
