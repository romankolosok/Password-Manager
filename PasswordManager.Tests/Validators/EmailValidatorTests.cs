using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class EmailValidatorTests
    {
        private readonly EmailValidator _validator = new();

        private static EmailInput Email(string email)
        {
            return new EmailInput { Email = email };
        }

        [Fact]
        public void ValidEmailReturnsSuccess()
        {
            var input = Email("email@example.com");
            var result = _validator.Validate(input);
            Assert.True(result.IsValid);
        }

        public static IEnumerable<object[]> InvalidEmails()
        {
            yield return new object[] { "plainaddress" };
            yield return new object[] { "@missingusername.com" };
            yield return new object[] { "username@.com" };
            yield return new object[] { "username@com" };
            yield return new object[] { "username@domain..com" };
            yield return new object[] { "username.com" };
        }

        [Theory]
        [MemberData(nameof(InvalidEmails))]
        public void InvalidEmailReturnsFailure(string email)
        {
            var input = Email(email);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Email must be a valid email address.", result.Errors.Select(e => e.ErrorMessage));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EmptyEmailReturnsFailure(string email)
        {
            var input = Email(email);
            var result = _validator.Validate(input);
            Assert.False(result.IsValid);
            Assert.Contains("Email cannot be empty.", result.Errors.Select(e => e.ErrorMessage));
        }

        [Fact]
        public void MaxLengthEmailReturnsSuccess()
        {
            var emailString = new string('a', 244) + "@example.com"; // 244 + 12 = 256 characters
            var email = Email(emailString);
            var result = _validator.Validate(email);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void MaxLengthExceededEmailReturnsFailure()
        {
            var emailString = new string('a', 245) + "@example.com"; // 245 + 12 = 257 characters
            var email = Email(emailString);
            var result = _validator.Validate(email);
            Assert.False(result.IsValid);
            Assert.Contains("Email must not exceed 256 characters.", result.Errors.Select(e => e.ErrorMessage));

        }
    }
}
