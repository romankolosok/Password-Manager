using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Services
{
    public class UserProfileServiceTests
    {

        private Mock<IVaultRepository> _vaultRepository { get; } = new();

        public UserProfileService CreateService() => new(_vaultRepository.Object);

        private UserProfileEntity MakeEntity(Guid? id = null, string? salt = null, string? evt = null)
        {
            return new()
            {
                Id = id ?? TestData.UserId(),
                Salt = salt ?? TestData.AccessToken(),
                EncryptedDEK = evt ?? TestData.AccessToken(),
                CreatedAt = DateTime.UtcNow,
            };
        }

        [Fact]
        public async Task CreateProfileAsyncReturnsSuccessWhenRepoAddsNewUser()
        {
            _vaultRepository.Reset();

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.CreateUserProfileAsync(user))
                .Returns(Task.CompletedTask);

            var result = await CreateService().CreateProfileAsync(user);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task CreateProfileAsyncReturnsFailureWhenRepoThrowsRepositoryException()
        {
            _vaultRepository.Reset();
            var errorMsg = "db unavailable";

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.CreateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new RepositoryException(errorMsg));

            var result = await CreateService().CreateProfileAsync(user);

            Assert.False(result.Success);
            Assert.Equal($"Database error while creating profile: {errorMsg}", result.Message);
        }

        [Fact]
        public async Task CreateProfileAsyncReturnsFailureWhenRepoThrowsGenericException()
        {
            _vaultRepository.Reset();
            var errorMsg = "db unavailable";

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.CreateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new Exception(errorMsg));

            var result = await CreateService().CreateProfileAsync(user);

            Assert.False(result.Success);
            Assert.Equal($"Failed to create user profile: {errorMsg}", result.Message);
        }


        [Fact]
        public async Task GetProfileAsyncReturnsProfileWhenProfileExists()
        {
            _vaultRepository.Reset();

            var userId = TestData.UserId();

            var user = MakeEntity(userId);
            _vaultRepository
                .Setup(r => r.GetUserProfileAsync(userId))
                .ReturnsAsync(user);

            var result = await CreateService().GetProfileAsync(userId);

            Assert.True(result.Success);
            Assert.Equal(user, result.Value);
        }

        [Fact]
        public async Task GetProfileAsyncReturnsFailureWhenProfileDoesNotExist()
        {
            _vaultRepository.Reset();

            var userId = TestData.UserId();

            _vaultRepository
                .Setup(r => r.GetUserProfileAsync(userId))
                .ReturnsAsync((UserProfileEntity)null!);

            var result = await CreateService().GetProfileAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("User profile not found.", result.Message);
        }

        [Fact]
        public async Task GetProfileAsyncReturnsFailureWhenRepoThrowsRepositoryException()
        {
            _vaultRepository.Reset();
            var errorMsg = "db unavailable";

            var userId = TestData.UserId();
            _vaultRepository
                .Setup(r => r.GetUserProfileAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new RepositoryException(errorMsg));

            var result = await CreateService().GetProfileAsync(userId);

            Assert.False(result.Success);
            Assert.Equal($"Database error while fetching profile: {errorMsg}", result.Message);
        }

        [Fact]
        public async Task GetProfileAsyncReturnsFailureWhenRepoThrowsGenericException()
        {
            _vaultRepository.Reset();
            var errorMsg = "db unavailable";

            var userId = TestData.UserId();
            _vaultRepository
                .Setup(r => r.GetUserProfileAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception(errorMsg));

            var result = await CreateService().GetProfileAsync(userId);

            Assert.False(result.Success);
            Assert.Equal($"Failed to get user profile: {errorMsg}", result.Message);
        }

        [Fact]
        public async Task UpdateProfileAsyncReturnsSuccessWhenRepoUpdatesProfile()
        {
            _vaultRepository.Reset();

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.UpdateUserProfileAsync(user))
                .Returns(Task.CompletedTask);

            var result = await CreateService().UpdateProfileAsync(user);

            Assert.True(result.Success);
            _vaultRepository.Verify(r => r.UpdateUserProfileAsync(user), Times.Once);
        }

        [Fact]
        public async Task UpdateProfileAsyncReturnsFailureWhenRepoThrowsRepositoryException()
        {
            _vaultRepository.Reset();
            var errorMsg = "constraint violation";

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new RepositoryException(errorMsg));

            var result = await CreateService().UpdateProfileAsync(user);

            Assert.False(result.Success);
            Assert.Equal($"Database error while updating profile: {errorMsg}", result.Message);
        }

        [Fact]
        public async Task UpdateProfileAsyncReturnsFailureWhenRepoThrowsGenericException()
        {
            _vaultRepository.Reset();
            var errorMsg = "network failure";

            var user = MakeEntity();
            _vaultRepository
                .Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfileEntity>()))
                .ThrowsAsync(new Exception(errorMsg));

            var result = await CreateService().UpdateProfileAsync(user);

            Assert.False(result.Success);
            Assert.Equal($"Failed to update user profile: {errorMsg}", result.Message);
        }
    }
}
