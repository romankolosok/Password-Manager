using PasswordManager.Core.Models;

namespace PasswordManager.Tests.Models
{
    public class ResultTests
    {
        [Fact]
        public void TestResultOkReturnsSuccess()
        {
            var result = Result.Ok();

            Assert.True(result.Success);
        }

        [Fact]
        public void TestResultOkWithValueReturnsSuccessAndValue()
        {
            var result = Result<int>.Ok(42);
            Assert.True(result.Success);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void TestResultFailReturnsFailure()
        {
            var result = Result.Fail("Error occurred");
            Assert.False(result.Success);
            Assert.Equal("Error occurred", result.Message);
        }

        [Theory]
        [MemberData(nameof(GetDefaultValueTestData))]
        public void TestResultFailReturnsDefaultValueForType<T>(T expectedDefault)
        {
            var result = Result<T>.Fail("Error occurred");
            Assert.False(result.Success);
            Assert.Equal("Error occurred", result.Message);
            Assert.Equal(expectedDefault, result.Value);
        }

        [Theory]
        [MemberData(nameof(GetPolymorphismSuccessTestData))]
        public void TestResultGenericCanBeAssignedToBaseResult<T>(Result<T> genericResult, T expectedValue)
        {
            Result result = genericResult;

            Assert.True(result.Success);
            Assert.IsType<Result<T>>(result);
            Assert.Equal(expectedValue, ((Result<T>)result).Value);
        }

        [Theory]
        [MemberData(nameof(GetPolymorphismFailureTestData))]
        public void TestResultGenericFailureCanBeAssignedToBaseResult<T>(Result<T> genericResult, string expectedMessage)
        {
            Result result = genericResult;

            Assert.False(result.Success);
            Assert.Equal(expectedMessage, result.Message);
            Assert.IsType<Result<T>>(result);
        }

        public static IEnumerable<object[]> GetPolymorphismSuccessTestData()
        {
            yield return new object[] { Result<int>.Ok(42), 42 };
            yield return new object[] { Result<string>.Ok("test"), "test" };
            yield return new object[] { Result<bool>.Ok(true), true };
            yield return new object[] { Result<decimal>.Ok(99.99m), 99.99m };
        }

        public static IEnumerable<object[]> GetPolymorphismFailureTestData()
        {
            yield return new object[] { Result<int>.Fail("Error occurred"), "Error occurred" };
            yield return new object[] { Result<string>.Fail("String error"), "String error" };
            yield return new object[] { Result<bool>.Fail("Bool error"), "Bool error" };
            yield return new object[] { Result<decimal>.Fail("Decimal error"), "Decimal error" };
        }

        public static IEnumerable<object[]> GetDefaultValueTestData()
        {
            yield return new object[] { default(int) };
            yield return new object[] { default(string)! };
            yield return new object[] { default(bool) };
            yield return new object[] { default(decimal) };
            yield return new object[] { default(DateTime) };
        }


    }
}
