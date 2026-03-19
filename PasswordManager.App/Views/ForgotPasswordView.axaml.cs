using Avalonia.Controls;
using Avalonia.Interactivity;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    public partial class ForgotPasswordView : Window
    {
        private ForgotPasswordViewModel? _subscribedVm;

        public IAuthCoordinator? Coordinator { get; set; }

        public ForgotPasswordView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedVm != null)
                _subscribedVm.RecoveryEmailSent -= OnRecoveryEmailSent;

            _subscribedVm = DataContext as ForgotPasswordViewModel;
            if (_subscribedVm != null)
                _subscribedVm.RecoveryEmailSent += OnRecoveryEmailSent;
        }

        private void OnRecoveryEmailSent(object? sender, System.EventArgs e)
        {
            if (sender is ForgotPasswordViewModel vm)
                Coordinator?.OnForgotPasswordSuccess(this, vm.Email.Trim());
        }

        private void BackToLogin_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

