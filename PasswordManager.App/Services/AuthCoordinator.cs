using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Views;
using System.Windows;

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

        public void OnRegisterSuccess(Window registerWindow)
        {
            registerWindow.Close();
            _loginWindow?.Show();
            _loginWindow?.Activate();
        }
    }
}
