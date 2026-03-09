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
            {
                _subscribedVm.LoginSuccessful -= OnLoginSuccessful;
                _subscribedVm.EmailNotConfirmed -= OnEmailNotConfirmed;
            }

            _subscribedVm = DataContext as LoginViewModel;
            if (_subscribedVm != null)
            {
                _subscribedVm.LoginSuccessful += OnLoginSuccessful;
                _subscribedVm.EmailNotConfirmed += OnEmailNotConfirmed;
            }
        }

        private void OnLoginSuccessful(object? sender, System.EventArgs e)
        {
            Coordinator?.OnLoginSuccess(this);
        }

        private void OnEmailNotConfirmed(object? sender, System.EventArgs e)
        {
            if (sender is LoginViewModel vm)
                Coordinator?.RequestConfirmOtpFromLogin(this, vm.Email.Trim());
        }

        private void RegisterButton_Click(object? sender, RoutedEventArgs e)
        {
            Coordinator?.RequestRegister(this);
        }
    }
}
