using FluentAssertions;
using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Models
{
    public class PasswordOptionsTests
    {
        [Fact]
        public void DefaultValuesAreLength20AllIncludeTrueExcludeAmbiguousFalse()
        {
            var options = new PasswordOptions();

            options.Length.Should().Be(20);
            options.IncludeUppercase.Should().BeTrue();
            options.IncludeLowercase.Should().BeTrue();
            options.IncludeDigits.Should().BeTrue();
            options.IncludeSpecialCharacters.Should().BeTrue();
            options.ExcludeAmbiguousCharacters.Should().BeFalse();
        }
    }
}
