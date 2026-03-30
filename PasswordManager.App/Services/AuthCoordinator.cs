using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Views;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App.Services
{
    internal class AuthCoordinator : IAuthCoordinator
    {
        private readonly IServiceProvider _serviceProvider;
        private Window? _loginWindow;
        private bool _promptRecoveryKeyAfterNextLogin;
        private Window? _pendingSignupRegisterWindow;
        private string? _pendingSignupEmail;
        private string? _pendingSignupMasterPassword;
        private bool _signupFlowCompleted;


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

        public async void OnLoginSuccess(Window loginWindow)
        {
            if (_promptRecoveryKeyAfterNextLogin)
            {
                _promptRecoveryKeyAfterNextLogin = false;

                var authService = _serviceProvider.GetRequiredService<IAuthService>();
                var recoveryKeyResult = await authService.SetupRecoveryKeyAsync();
                if (recoveryKeyResult.Success && !string.IsNullOrWhiteSpace(recoveryKeyResult.Value))
                {
                    var recoveryKeyView = _serviceProvider.GetRequiredService<RecoveryKeyView>();
                    var recoveryKeyVm = _serviceProvider.GetRequiredService<ViewModels.RecoveryKeyViewModel>();
                    recoveryKeyVm.RecoveryKey = recoveryKeyResult.Value;
                    recoveryKeyView.DataContext = recoveryKeyVm;

                    // Block progress into the app until the user acknowledges saving the key.
                    await recoveryKeyView.ShowDialog<bool?>(loginWindow);
                }
            }

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

        public void OnRegisterSuccess(Window registerWindow, string email, string masterPassword)
        {
            registerWindow.Hide();
            _pendingSignupRegisterWindow = registerWindow;
            _pendingSignupEmail = email;
            _pendingSignupMasterPassword = masterPassword;
            _signupFlowCompleted = false;

            var confirmOtpView = _serviceProvider.GetRequiredService<ConfirmOtpView>();
            confirmOtpView.Closed += (_, _) =>
            {
                if (_signupFlowCompleted)
                    return;

                registerWindow.Close();
                _pendingSignupRegisterWindow = null;
                _pendingSignupEmail = null;
                _pendingSignupMasterPassword = null;

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

        public async void OnConfirmOtpSuccess(Window confirmOtpWindow)
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

            if (confirmOtpVm?.Purpose == ViewModels.ConfirmOtpViewModel.OtpPurpose.SignupConfirmation
                && !string.IsNullOrWhiteSpace(_pendingSignupEmail)
                && !string.IsNullOrWhiteSpace(_pendingSignupMasterPassword))
            {
                var authService = _serviceProvider.GetRequiredService<IAuthService>();
                var email = _pendingSignupEmail;
                var password = _pendingSignupMasterPassword;

                _pendingSignupEmail = null;
                _pendingSignupMasterPassword = null;

                var loginResult = await authService.LoginAsync(email!, password!);
                if (loginResult.Success)
                {
                    var recoveryKeyResult = await authService.SetupRecoveryKeyAsync();
                    var recoveryKeyView = _serviceProvider.GetRequiredService<RecoveryKeyView>();
                    var recoveryKeyVm = _serviceProvider.GetRequiredService<ViewModels.RecoveryKeyViewModel>();

                    if (recoveryKeyResult.Success && !string.IsNullOrWhiteSpace(recoveryKeyResult.Value))
                    {
                        recoveryKeyVm.RecoveryKey = recoveryKeyResult.Value;
                    }
                    else
                    {
                        recoveryKeyVm.RecoveryKey = string.Empty;
                        recoveryKeyVm.ErrorMessage = recoveryKeyResult.Message ?? "Failed to generate recovery key.";
                    }

                    recoveryKeyView.DataContext = recoveryKeyVm;
                    await recoveryKeyView.ShowDialog<bool?>(confirmOtpWindow);

                    _signupFlowCompleted = true;
                    _pendingSignupRegisterWindow?.Close();
                    _pendingSignupRegisterWindow = null;

                    var main = MainWindow;
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        desktop.MainWindow = main;

                    main.Show();
                    main.Activate();
                    confirmOtpWindow.Close();
                    _loginWindow?.Close();
                    _loginWindow = null;
                    return;
                }

                _promptRecoveryKeyAfterNextLogin = true;
                confirmOtpWindow.Close();
                _pendingSignupRegisterWindow?.Close();
                _pendingSignupRegisterWindow = null;
                _loginWindow?.Show();
                _loginWindow?.Activate();
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

        public void ShowChangePassword()
        {
            var vm = _serviceProvider.GetRequiredService<ViewModels.ChangePasswordViewModel>();
            var view = _serviceProvider.GetRequiredService<ChangePasswordView>();
            view.DataContext = vm;

            var window = new Window
            {
                Title = "Crypty - Change Password",
                Width = 400,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = view
            };

            vm.ChangePasswordSuccessful += (_, _) => window.Close();

            _ = window.ShowDialog(MainWindow);
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
                // When user closes/finishes OTP flow, return to login.
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
