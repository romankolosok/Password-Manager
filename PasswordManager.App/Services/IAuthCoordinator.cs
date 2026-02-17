using System.Windows;

namespace PasswordManager.App.Services
{
    /// <summary>
    /// Coordinates auth flow: login window, register window, and transition to main window.
    /// </summary>
    public interface IAuthCoordinator
    {
        /// <summary>
        /// Shows the login window (called at app startup).
        /// </summary>
        void ShowLogin();

        /// <summary>
        /// Called when login succeeds. Closes auth windows and shows main window.
        /// </summary>
        void OnLoginSuccess(Window loginWindow);

        /// <summary>
        /// Opens the register window (optionally hiding the login window). When register window closes, login is shown again.
        /// </summary>
        void RequestRegister(Window loginWindow);

        /// <summary>
        /// Called when registration succeeds. Closes register window and shows login so the user can sign in.
        /// </summary>
        void OnRegisterSuccess(Window registerWindow);
    }
}
