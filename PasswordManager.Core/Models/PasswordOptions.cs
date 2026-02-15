namespace PasswordManager.Core.Models
{
    public class PasswordOptions
    {
        public int Length { get; set; } = 20;
        public bool IncludeUppercase { get; set; } = true;
        public bool IncludeLowercase { get; set; } = true;
        public bool IncludeDigits { get; set; } = true;
        public bool IncludeSpecialCharacters { get; set; } = true;
        public bool ExcludeAmbiguousCharacters { get; set; } = false;
    }
}