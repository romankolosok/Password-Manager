using PasswordManager.Core.Models;
using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class PasswordValidatorTests
    {
        private readonly PasswordValidator _validator = new();

        private static PasswordInput Password(string password)
        {
            return new PasswordInput { Password = password };
        }

        [Theory]
        [InlineData("")]
        [InlineData("         ")]
        [InlineData(null)]
        public void EmptyPasswordReturnsFailure(string password)
        {
            var input = Password(password);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Password cannot be empty.", result.Errors.Select(e => e.ErrorMessage));
        }

        public static IEnumerable<object[]> TooShortPasswords()
        {
            yield return new object[] { "a" };
            yield return new object[] { new string('a', Random.Shared.Next(2, PasswordPolicy.MinLength - 2)) };
            yield return new object[] { new string('a', PasswordPolicy.MinLength - 1) };
        }

        public static IEnumerable<object[]> TooLongPasswords()
        {
            yield return new object[] { new string('a', PasswordPolicy.MaxLength + 1) };
            yield return new object[] { new string('a', PasswordPolicy.MaxLength + Random.Shared.Next(100)) };
        }


        [Theory]
        [MemberData(nameof(TooShortPasswords))]
        public void TooShortPasswordReturnsFailure(string password)
        {
            var input = Password(password);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains($"Password must be at least {PasswordPolicy.MinLength} characters.", result.Errors.Select(e => e.ErrorMessage));
        }

        [Theory]
        [MemberData(nameof(TooLongPasswords))]
        public void TooLongPasswordReturnsFailure(string password)
        {
            var input = Password(password);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains($"Password must not exceed {PasswordPolicy.MaxLength} characters.", result.Errors.Select(e => e.ErrorMessage));
        }

    }
}
