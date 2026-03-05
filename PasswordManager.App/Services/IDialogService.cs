using System.Threading.Tasks;

namespace PasswordManager.App.Services
{
    public interface IDialogService
    {
        Task<bool> ConfirmAsync(string message, string title);
    }
}
