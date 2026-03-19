using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Views;

namespace PasswordManager.App.Services
{
    internal class AuthCoordinator : IAuthCoordinator
    {
        private readonly IServiceProvider _serviceProvider;
        private Window? _loginWindow;


        public AuthCoordinator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public MainWindow MainWindow => _serviceProvider.GetRequiredService<MainWindow>();

        public void ShowLogin()
        {
            var vm = _serviceProvider.GetRequiredService<ViewModels.LoginViewModel>();
            var view = _serviceProvider.GetRequiredService<LoginView>();
            view.DataContext = vm;
            view.Coordinator = this;
            _loginWindow = view;
            view.Show();
        }

        public void OnLoginSuccess(Window loginWindow)
        {
            var main = MainWindow;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = main;

            main.Show();
            main.Activate();
            loginWindow.Close();
            _loginWindow = null;
        }

        public void RequestRegister(Window loginWindow)
        {
            _loginWindow = loginWindow;
            loginWindow.Hide();

            var registerView = _serviceProvider.GetRequiredService<RegisterView>();
            registerView.Closed += (_, _) =>
            {
                loginWindow.Show();
                _loginWindow = loginWindow;
            };
            var registerVm = _serviceProvider.GetRequiredService<ViewModels.RegisterViewModel>();
            registerView.DataContext = registerVm;
            registerView.Coordinator = this;
            registerView.Show();
        }

        public void OnRegisterSuccess(Window registerWindow, string email)
        {
            registerWindow.Hide();

            var confirmOtpView = _serviceProvider.GetRequiredService<ConfirmOtpView>();
            confirmOtpView.Closed += (_, _) =>
            {
                registerWindow.Close();
                _loginWindow?.Show();
                _loginWindow?.Activate();
            };
            var confirmOtpVm = _serviceProvider.GetRequiredService<ViewModels.ConfirmOtpViewModel>();
            confirmOtpVm.Email = email;
            confirmOtpVm.Purpose = ViewModels.ConfirmOtpViewModel.OtpPurpose.SignupConfirmation;
            confirmOtpView.DataContext = confirmOtpVm;
            confirmOtpView.Coordinator = this;
            confirmOtpView.Show();
        }

        public void OnConfirmOtpSuccess(Window confirmOtpWindow)
        {
            var confirmOtpVm = confirmOtpWindow.DataContext as ViewModels.ConfirmOtpViewModel;

            if (confirmOtpVm?.Purpose == ViewModels.ConfirmOtpViewModel.OtpPurpose.PasswordRecovery)
            {
                confirmOtpWindow.Close();

                var setNewPasswordView = _serviceProvider.GetRequiredService<SetNewPasswordView>();
                setNewPasswordView.Closed += (_, _) =>
                {
                    _loginWindow?.Show();
                    _loginWindow?.Activate();
                };

                var setNewPasswordVm = _serviceProvider.GetRequiredService<ViewModels.SetNewPasswordViewModel>();
                setNewPasswordVm.Email = confirmOtpVm.Email;
                setNewPasswordView.DataContext = setNewPasswordVm;
                setNewPasswordView.Coordinator = this;
                setNewPasswordView.Show();
                return;
            }

            confirmOtpWindow.Close();
            _loginWindow?.Show();
            _loginWindow?.Activate();
        }

        public void OnSetNewPasswordSuccess(Window setNewPasswordWindow)
        {
            setNewPasswordWindow.Close();
            _loginWindow?.Show();
            _loginWindow?.Activate();
        }

        public void RequestConfirmOtpFromLogin(Window loginWindow, string email)
        {
            _loginWindow = loginWindow;
            loginWindow.Hide();

            var confirmOtpView = _serviceProvider.GetRequiredService<ConfirmOtpView>();
            confirmOtpView.Closed += (_, _) =>
            {
                loginWindow.Show();
                _loginWindow = loginWindow;
            };
            var confirmOtpVm = _serviceProvider.GetRequiredService<ViewModels.ConfirmOtpViewModel>();
            confirmOtpVm.Email = email;
            confirmOtpVm.Purpose = ViewModels.ConfirmOtpViewModel.OtpPurpose.SignupConfirmation;
            confirmOtpView.DataContext = confirmOtpVm;
            confirmOtpView.Coordinator = this;
            confirmOtpView.Show();
        }

        public void RequestForgotPassword(Window loginWindow)
        {
            _loginWindow = loginWindow;
            loginWindow.Hide();

            var forgotPasswordView = _serviceProvider.GetRequiredService<ForgotPasswordView>();
            forgotPasswordView.Closed += (_, _) =>
            {
                loginWindow.Show();
                _loginWindow = loginWindow;
            };

            var forgotPasswordVm = _serviceProvider.GetRequiredService<ViewModels.ForgotPasswordViewModel>();
            forgotPasswordView.DataContext = forgotPasswordVm;
            forgotPasswordView.Coordinator = this;
            forgotPasswordView.Show();
        }

        public void OnForgotPasswordSuccess(Window forgotPasswordWindow, string email)
        {
            forgotPasswordWindow.Hide();

            var confirmOtpView = _serviceProvider.GetRequiredService<ConfirmOtpView>();
            confirmOtpView.Closed += (_, _) =>
            {
                // When user closes/finishes OTP flow, return to login (AuthCoordinator handles it too).
                forgotPasswordWindow.Close();
            };

            var confirmOtpVm = _serviceProvider.GetRequiredService<ViewModels.ConfirmOtpViewModel>();
            confirmOtpVm.Email = email;
            confirmOtpVm.Purpose = ViewModels.ConfirmOtpViewModel.OtpPurpose.PasswordRecovery;
            confirmOtpView.DataContext = confirmOtpVm;
            confirmOtpView.Coordinator = this;
            confirmOtpView.Show();
        }
    }
}
