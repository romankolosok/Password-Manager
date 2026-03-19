using Avalonia.Controls;
using Avalonia.Interactivity;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    public partial class RegisterView : Window
    {
        private RegisterViewModel? _subscribedVm;

        public IAuthCoordinator? Coordinator { get; set; }

        public RegisterView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedVm != null)
                _subscribedVm.RegisterSuccessful -= OnRegisterSuccessful;

            _subscribedVm = DataContext as RegisterViewModel;
            if (_subscribedVm != null)
                _subscribedVm.RegisterSuccessful += OnRegisterSuccessful;
        }

        private void OnRegisterSuccessful(object? sender, System.EventArgs e)
        {
            if (sender is RegisterViewModel vm)
                Coordinator?.OnRegisterSuccess(this, vm.Email, vm.MasterPassword);
        }

        private void BackToLogin_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
