using Avalonia.Controls;

namespace PasswordManager.App.Services
{
    public interface IAuthCoordinator
    {
        void ShowLogin();

        void OnLoginSuccess(Window loginWindow);

        void RequestRegister(Window loginWindow);

        void OnRegisterSuccess(Window registerWindow);
    }
}
