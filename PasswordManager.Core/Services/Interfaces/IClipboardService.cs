namespace PasswordManager.Core.Services.Interfaces
{
    public interface IClipboardService
    {
        void CopyWithAutoClear(string text, int secondsToClear = 15);
        void ClearClipboard();
    }
}
