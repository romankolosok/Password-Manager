using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;

namespace PasswordManager.Tests.Services
{
    public class PasswordGeneratorTests
    {
        private readonly PasswordGenerator _generator = new();

        private const string Ambiguous = "0OolI1|`'\"";

        [Fact]
        public void GenerateWithDefaultOptionsReturnsAllFourCharTypes()
        {
            var result = _generator.Generate(new PasswordOptions());

            Assert.True(result.Success);
            Assert.Equal(20, result.Value.Length);
            Assert.Contains(result.Value, char.IsUpper);
            Assert.Contains(result.Value, char.IsLower);
            Assert.Contains(result.Value, char.IsDigit);
            Assert.Contains(result.Value, c => PasswordPolicy.SpecialCharacters.Contains(c));
        }

        [Fact]
        public void GenerateWithMinLengthReturnsPasswordOfLength12()
        {
            var options = new PasswordOptions { Length = 12 };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.Equal(12, result.Value.Length);
        }

        [Fact]
        public void GenerateWithMaxLengthReturnsPasswordOfLength128()
        {
            var options = new PasswordOptions { Length = 128 };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.Equal(128, result.Value.Length);
        }

        [Fact]
        public void GenerateWithLengthBelowMinReturnsFailure()
        {
            var options = new PasswordOptions { Length = 11 };

            var result = _generator.Generate(options);

            Assert.False(result.Success);
        }

        [Fact]
        public void GenerateWithLengthAboveMaxReturnsFailure()
        {
            var options = new PasswordOptions { Length = 129 };

            var result = _generator.Generate(options);

            Assert.False(result.Success);
        }

        [Fact]
        public void GenerateWithOnlyUppercaseReturnsUppercaseOnly()
        {
            var options = new PasswordOptions
            {
                IncludeUppercase = true,
                IncludeLowercase = false,
                IncludeDigits = false,
                IncludeSpecialCharacters = false
            };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.All(result.Value, c => Assert.True(char.IsUpper(c)));
        }

        [Fact]
        public void GenerateWithOnlyLowercaseReturnsLowercaseOnly()
        {
            var options = new PasswordOptions
            {
                IncludeUppercase = false,
                IncludeLowercase = true,
                IncludeDigits = false,
                IncludeSpecialCharacters = false
            };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.All(result.Value, c => Assert.True(char.IsLower(c)));
        }

        [Fact]
        public void GenerateWithOnlyDigitsReturnsDigitsOnly()
        {
            var options = new PasswordOptions
            {
                IncludeUppercase = false,
                IncludeLowercase = false,
                IncludeDigits = true,
                IncludeSpecialCharacters = false
            };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.All(result.Value, c => Assert.True(char.IsDigit(c)));
        }

        [Fact]
        public void GenerateWithOnlySpecialReturnsSpecialOnly()
        {
            var options = new PasswordOptions
            {
                IncludeUppercase = false,
                IncludeLowercase = false,
                IncludeDigits = false,
                IncludeSpecialCharacters = true
            };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.All(result.Value, c => Assert.True(PasswordPolicy.SpecialCharacters.Contains(c)));
        }

        [Fact]
        public void GenerateWithAllFlagsFalseReturnsFailure()
        {
            var options = new PasswordOptions
            {
                IncludeUppercase = false,
                IncludeLowercase = false,
                IncludeDigits = false,
                IncludeSpecialCharacters = false
            };

            var result = _generator.Generate(options);

            Assert.False(result.Success);
        }

        [Fact]
        public void GenerateWithAllTrueAndExcludeAmbiguousContainsNoAmbiguousChars()
        {
            var options = new PasswordOptions
            {
                Length = 128,
                IncludeUppercase = true,
                IncludeLowercase = true,
                IncludeDigits = true,
                IncludeSpecialCharacters = true,
                ExcludeAmbiguousCharacters = true
            };

            var result = _generator.Generate(options);

            Assert.True(result.Success);
            Assert.Contains(result.Value, char.IsUpper);
            Assert.Contains(result.Value, char.IsLower);
            Assert.Contains(result.Value, char.IsDigit);
            Assert.Contains(result.Value, c => PasswordPolicy.SpecialCharacters.Contains(c));
            Assert.All(result.Value, c => Assert.DoesNotContain(c.ToString(), Ambiguous));
        }

        [Fact]
        public void GenerateProducesUniqueResultsAcrossMultipleCalls()
        {
            var options = new PasswordOptions();

            var passwords = Enumerable.Range(0, 10)
                .Select(_ => _generator.Generate(options))
                .ToList();

            Assert.All(passwords, r => Assert.True(r.Success));

            var distinct = passwords.Select(r => r.Value).Distinct().Count();
            Assert.Equal(10, distinct);
        }
    }
}
