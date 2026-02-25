using FluentAssertions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Validators;

namespace PasswordManager.Tests.Validators
{
    public class PasswordOptionsValidatorTests
    {
        private readonly PasswordOptionsValidator _validator = new();

        private static PasswordOptions Options(int length, bool atLeastOneCharType)
        {
            return new PasswordOptions
            {
                Length = length,
                IncludeUppercase = atLeastOneCharType,
                IncludeLowercase = false,
                IncludeDigits = false,
                IncludeSpecialCharacters = false
            };
        }

        private static PasswordOptions Options(int length, bool upper, bool lower, bool digits, bool special)
        {
            return new PasswordOptions
            {
                Length = length,
                IncludeUppercase = upper,
                IncludeLowercase = lower,
                IncludeDigits = digits,
                IncludeSpecialCharacters = special
            };
        }

        private static PasswordOptions Options(int length, bool upper, bool lower, bool digits, bool special, bool excludeAmbiguous)
        {
            return new PasswordOptions
            {
                Length = length,
                IncludeUppercase = upper,
                IncludeLowercase = lower,
                IncludeDigits = digits,
                IncludeSpecialCharacters = special,
                ExcludeAmbiguousCharacters = excludeAmbiguous
            };
        }


        public static IEnumerable<object[]> PairwiseValidOptions => new List<object[]>
        {
            new object[] { 12, true, true, true, true, false },
            new object[] { 128, false, false, false, true, true },
            new object[] { 70, true, false, false, false, true },
            new object[] { 20, false, true, false, false, false },
            new object[] { 12, false, false, true, false, true },
            new object[] { 128, true, true, false, true, false },
            new object[] { 70, true, false, true, false, false },
            new object[] { 20, false, true, true, false, true },
            new object[] { 12, true, true, true, false, false },
            new object[] { 128, false, false, true, true, false },
            new object[] { 70, false, true, false, true, true },
            new object[] { 20, true, false, false, true, false },
            new object[] { 12, false, true, true, true, false },
            new object[] { 128, true, false, true, false, true },
            new object[] { 70, true, true, false, false, true },
        };

        [Theory]
        [MemberData(nameof(PairwiseValidOptions))]
        public void PairwiseValidCombinationsPassValidation(
            int length,
            bool includeUppercase,
            bool includeLowercase,
            bool includeDigits,
            bool includeSpecialCharacters,
            bool excludeAmbiguousCharacters)
        {
            var options = Options(length, includeUppercase, includeLowercase, includeDigits, includeSpecialCharacters, excludeAmbiguousCharacters);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeTrue(
                "length={0}, U={1}, Lw={2}, D={3}, S={4}, ExAmb={5}",
                length, includeUppercase, includeLowercase, includeDigits, includeSpecialCharacters, excludeAmbiguousCharacters);
        }


        [Fact]
        public void DecisionTableR1LengthInRangeAtLeastOneCharTypeValid()
        {
            var options = Options(20, true, true, true, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeTrue();
        }


        [Theory]
        [InlineData(20, 1)]
        [InlineData(11, 2)]
        public void DecisionTableNoCharTypeInvalid(int length, int expectedErrorCount)
        {
            var options = Options(length, false, false, false, false);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(expectedErrorCount);
            result.Errors.Should().Contain(e => e.ErrorMessage.Equals("At least one character set must be selected."));
            if (expectedErrorCount == 2)
                result.Errors.Should().Contain(e =>
                    e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
                );
        }


        [Theory]
        [InlineData(11)]
        [InlineData(129)]
        public void DecisionTableLengthOutOfRangeWithCharTypeInvalid(int length)
        {
            var options = Options(length, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void LengthBelowMinimum11Invalid()
        {
            var options = Options(11, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void LengthAtMinimum12Valid()
        {
            var options = Options(12, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void LengthWithinRange20Valid()
        {
            var options = Options(20, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void LengthAtMaximum128Valid()
        {
            var options = Options(128, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void LengthAboveMaximum129Invalid()
        {
            var options = Options(129, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void LengthZeroInvalid()
        {
            var options = Options(0, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void LengthNegativeInvalid()
        {
            var options = Options(-1, true);

            var result = _validator.Validate(options);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e =>
                e.ErrorMessage.Contains("Password length must be between", StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
