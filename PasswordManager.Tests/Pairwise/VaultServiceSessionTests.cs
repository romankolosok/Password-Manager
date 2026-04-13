using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fixtures.Pairwise;

namespace PasswordManager.Tests.Pairwise
{
    public class VaultServiceSessionTests : IClassFixture<PairwiseVaultSessionFixture>, IDisposable
    {
        private readonly PairwiseVaultSessionFixture _fixture;

        public VaultServiceSessionTests(PairwiseVaultSessionFixture fixture)
        {
            _fixture = fixture;
        }

        public void Dispose()
        {
            _fixture.Reset();
        }

        [Fact]
        public async Task OperationsFailWhenSessionIsInactive()
        {
            _fixture.Reset();

            var service = _fixture.CreateService();

            var result = await service.GetAllEntriesAsync();

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }

        [Fact]
        public async Task OperationsSucceedAfterSettingSession()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SessionService.SetUser(userId, "vault@example.com");
            _fixture.SessionService.SetDerivedKey(new byte[32]);

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>());

            var service = _fixture.CreateService();

            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task OperationsFailAfterSessionCleared()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            _fixture.SessionService.SetUser(userId, "vault@example.com");
            _fixture.SessionService.SetDerivedKey(new byte[32]);

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>());

            var service = _fixture.CreateService();
            _fixture.SessionService.ClearSession();

            var result = await service.GetAllEntriesAsync();

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }
    }
}
