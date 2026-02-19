namespace PasswordManager.Core.Services.Interfaces
{
    public interface IPasswordStrengthChecker
    {
        int CheckStrength(string password);
    }
}
