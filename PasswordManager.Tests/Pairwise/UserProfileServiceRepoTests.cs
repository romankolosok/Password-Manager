using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Tests.Fixtures.Pairwise;

namespace PasswordManager.Tests.Pairwise
{
    public class UserProfileServiceRepoTests : IClassFixture<PairwiseUserProfileRepoFixture>
    {
        private readonly PairwiseUserProfileRepoFixture _fixture;

        public UserProfileServiceRepoTests(PairwiseUserProfileRepoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateAndGetProfileRoundTrips()
        {
            var service = _fixture.CreateService();
            var id = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var entity = new UserProfileEntity
            {
                Id = id,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = Convert.ToBase64String(new byte[32]),
                CreatedAt = createdAt
            };

            var create = await service.CreateProfileAsync(entity);
            Assert.True(create.Success);

            var get = await service.GetProfileAsync(id);

            Assert.True(get.Success);
            Assert.Equal(entity.Id, get.Value!.Id);
            Assert.Equal(entity.Salt, get.Value.Salt);
            Assert.Equal(entity.EncryptedDEK, get.Value.EncryptedDEK);
            Assert.Equal(entity.CreatedAt, get.Value.CreatedAt);
        }

        [Fact]
        public async Task UpdateProfileModifiesStoredEntity()
        {
            var service = _fixture.CreateService();
            var id = Guid.NewGuid();
            var originalSalt = Convert.ToBase64String(new byte[16]);
            var profile = new UserProfileEntity
            {
                Id = id,
                Salt = originalSalt,
                EncryptedDEK = "dek-original",
                CreatedAt = DateTime.UtcNow
            };

            await service.CreateProfileAsync(profile);

            var newSalt = Convert.ToBase64String(new byte[16]);
            profile.Salt = newSalt;
            var update = await service.UpdateProfileAsync(profile);
            Assert.True(update.Success);

            var get = await service.GetProfileAsync(id);
            Assert.True(get.Success);
            Assert.Equal(newSalt, get.Value!.Salt);
        }

        [Fact]
        public async Task GetProfileForNonExistentIdReturnsFail()
        {
            var service = _fixture.CreateService();

            var result = await service.GetProfileAsync(Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Equal("User profile not found.", result.Message);
        }

        [Fact]
        public async Task DuplicateCreateReturnsFailWithDatabaseError()
        {
            var service = _fixture.CreateService();
            var id = Guid.NewGuid();
            var entity = new UserProfileEntity
            {
                Id = id,
                Salt = Convert.ToBase64String(new byte[16]),
                EncryptedDEK = "dek",
                CreatedAt = DateTime.UtcNow
            };

            var first = await service.CreateProfileAsync(entity);
            Assert.True(first.Success);

            var second = await service.CreateProfileAsync(entity);

            Assert.False(second.Success);
            Assert.Contains("Database error", second.Message, StringComparison.Ordinal);
        }
    }
}
