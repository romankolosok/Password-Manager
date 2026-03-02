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

        [Fact]
        public void TooShortPasswordDoesNotTriggerCharacterTypeRules()
        {
            var input = Password("abc");
            var result = _validator.Validate(input);
            var messages = result.Errors.Select(e => e.ErrorMessage).ToList();

            Assert.DoesNotContain("Password must contain at least one uppercase letter.", messages);
            Assert.DoesNotContain("Password must contain at least one lowercase letter.", messages);
            Assert.DoesNotContain("Password must contain at least one digit.", messages);
            Assert.DoesNotContain($"Password must contain at least one special character (e.g. from: {PasswordPolicy.SpecialCharacters}).", messages);
        }

        [Fact]
        public void MultipleViolationsReturnAllErrors()
        {
            var input = Password("abcdefghijkl");
            var result = _validator.Validate(input);
            var messages = result.Errors.Select(e => e.ErrorMessage).ToList();

            Assert.False(result.IsValid);
            Assert.Contains("Password must contain at least one uppercase letter.", messages);
            Assert.Contains("Password must contain at least one digit.", messages);
            Assert.Contains($"Password must contain at least one special character (e.g. from: {PasswordPolicy.SpecialCharacters}).", messages);
        }

        [Theory]
        [InlineData("abcdefgh1!xy", "Password must contain at least one uppercase letter.")]
        [InlineData("ABCDEFGH1!XY", "Password must contain at least one lowercase letter.")]
        [InlineData("Abcdefghij!x", "Password must contain at least one digit.")]
        [InlineData("Abcdefghij1x", $"Password must contain at least one special character (e.g. from: {PasswordPolicy.SpecialCharacters}).")]
        public void MissingCharacterTypeReturnsFailure(string password, string expectedError)
        {
            var input = Password(password);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains(expectedError, result.Errors.Select(e => e.ErrorMessage));
        }

        [Theory]
        [InlineData("MyP@ssw0rd!!")]
        [InlineData("Str0ng!Passw0rd")]
        [InlineData("C0mpl3x#Passw0rd")]
        public void ValidPasswordReturnsSuccess(string password)
        {
            var result = _validator.Validate(Password(password));
            Assert.True(result.IsValid);
        }

        [Fact]
        public void MaxLengthPasswordWithAllTypesReturnsSuccess()
        {
            var password = "Ab1!" + new string('a', PasswordPolicy.MaxLength - 4);
            Assert.Equal(PasswordPolicy.MaxLength, password.Length);

            var result = _validator.Validate(Password(password));
            Assert.True(result.IsValid);
        }


        [Fact]
        public void MinLengthPasswordWithAllTypesReturnsSuccess()
        {
            var password = "Abcdefgh1!xy";
            Assert.Equal(PasswordPolicy.MinLength, password.Length);

            var result = _validator.Validate(Password(password));
            Assert.True(result.IsValid);
        }
    }
}
