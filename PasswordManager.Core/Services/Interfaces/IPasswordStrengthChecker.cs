namespace PasswordManager.Core.Services.Interfaces
{
    public interface IPasswordStrengthChecker
    {
        public int CheckStrength(string password);

        public string GetFeedback(string password);

        public string GetStrengthLabel(int strength);
    }
}
