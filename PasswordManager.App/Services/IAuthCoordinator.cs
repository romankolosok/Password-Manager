using Avalonia.Controls;

namespace PasswordManager.App.Services
{
    public interface IAuthCoordinator
    {
        void ShowLogin();

        void OnLoginSuccess(Window loginWindow);

        void RequestRegister(Window loginWindow);

        void OnRegisterSuccess(Window registerWindow, string email);

        void OnConfirmOtpSuccess(Window confirmOtpWindow);
        
        void RequestConfirmOtpFromLogin(Window loginWindow, string email);
    }
}