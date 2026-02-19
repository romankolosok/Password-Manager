using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Core.Services.Implementations
{
    public class ZxcvbnPasswordStrengthChecker : IPasswordStrengthChecker
    {
        public int CheckStrength(string password)
        {
            var result = Zxcvbn.Core.EvaluatePassword(password);
            return result.Score;
        }
    }
}
