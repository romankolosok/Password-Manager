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

        public string GetStrengthLabel(int strength)
        {
            return strength switch
            {
                0 => "Very Weak",
                1 => "Weak",
                2 => "Fair",
                3 => "Strong",
                4 => "Very Strong",
                _ => "Unknown"
            };
        }

        public string GetFeedback(string password)
        {
            var result = Zxcvbn.Core.EvaluatePassword(password);
            return string.Join("\n", result.Feedback.Suggestions);
        }
    }
}
