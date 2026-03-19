using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    public partial class SetNewPasswordView : Window
    {
        private SetNewPasswordViewModel? _subscribedVm;

        public IAuthCoordinator? Coordinator { get; set; }

        public SetNewPasswordView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_subscribedVm != null)
                _subscribedVm.ResetPasswordSuccessful -= OnResetPasswordSuccessful;

            _subscribedVm = DataContext as SetNewPasswordViewModel;
            if (_subscribedVm != null)
                _subscribedVm.ResetPasswordSuccessful += OnResetPasswordSuccessful;
        }

        private void OnResetPasswordSuccessful(object? sender, EventArgs e)
        {
            Coordinator?.OnSetNewPasswordSuccess(this);
        }

        private void BackToLogin_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

