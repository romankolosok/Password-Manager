using Avalonia.Controls;

namespace PasswordManager.App.Services
{
    public interface IAuthCoordinator
    {
        void ShowLogin();

        void OnLoginSuccess(Window loginWindow);

        void RequestRegister(Window loginWindow);

        void OnRegisterSuccess(Window registerWindow, string email, string masterPassword);

        void OnConfirmOtpSuccess(Window confirmOtpWindow);

        void RequestConfirmOtpFromLogin(Window loginWindow, string email);

        void RequestForgotPassword(Window loginWindow);

        void OnForgotPasswordSuccess(Window forgotPasswordWindow, string email);

        void OnSetNewPasswordSuccess(Window setNewPasswordWindow);

        void ShowChangePassword();
    }
}