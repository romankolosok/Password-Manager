using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fixtures.Pairwise;

namespace PasswordManager.Tests.Pairwise
{
    public class VaultServiceCryptoTests : IClassFixture<PairwiseVaultCryptoFixture>
    {
        private readonly PairwiseVaultCryptoFixture _fixture;

        public VaultServiceCryptoTests(PairwiseVaultCryptoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AddThenGetEntryRoundTripsPlaintext()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            const string password = "VaultPassword1!";
            var salt = new byte[16];
            Random.Shared.NextBytes(salt);
            var crypto = new CryptoService();
            var derivedKey = crypto.DeriveKey(password, salt);

            _fixture.SetupActiveSession(userId, derivedKey);

            VaultEntryEntity? captured = null;
            _fixture.VaultRepository
                .Setup(r => r.UpsertEntryAsync(It.IsAny<VaultEntryEntity>()))
                .Callback<VaultEntryEntity>(e => captured = e)
                .Returns(Task.CompletedTask);
            _fixture.VaultRepository
                .Setup(r => r.GetEntryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync((Guid uid, Guid entryId) =>
                    captured != null && uid == userId && entryId == captured.Id ? captured : null);

            var entry = new VaultEntry
            {
                WebsiteName = "Example",
                Username = "alice",
                Password = "secret",
                Url = "https://example.com",
                Notes = "n",
                Category = "c",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var service = _fixture.CreateService();

            var add = await service.AddEntryAsync(entry);
            Assert.True(add.Success);
            Assert.NotNull(captured);

            var get = await service.GetEntryAsync(captured!.Id.ToString());
            Assert.True(get.Success);
            Assert.Equal("Example", get.Value!.WebsiteName);
            Assert.Equal("alice", get.Value.Username);
            Assert.Equal("secret", get.Value.Password);
        }

        [Fact]
        public async Task GetAllEntriesSkipsCorruptedCiphertext()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            var crypto = new CryptoService();
            var salt = crypto.GenerateSalt();
            var key = crypto.DeriveKey("Master1!Password", salt);
            _fixture.SetupActiveSession(userId, key);

            var validPayloadJson =
                "{\"WebsiteName\":\"ok\",\"Username\":\"u\",\"Password\":\"p\",\"Url\":\"\",\"Notes\":\"\",\"Category\":\"\",\"IsFavorite\":false}";
            var validEncrypt = crypto.Encrypt(validPayloadJson, key);
            Assert.True(validEncrypt.Success);

            var validEntity = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = validEncrypt.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var corruptedEntity = new VaultEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "!!!not-valid-base64!!!",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity> { validEntity, corruptedEntity });

            var service = _fixture.CreateService();

            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Single(result.Value!);
            Assert.Equal("ok", result.Value[0].WebsiteName);
        }

        [Fact]
        public async Task DifferentEntriesProduceDifferentCiphertexts()
        {
            _fixture.Reset();
            var userId = Guid.NewGuid();
            var crypto = new CryptoService();
            var key = crypto.DeriveKey("SamePassword1!", crypto.GenerateSalt());
            _fixture.SetupActiveSession(userId, key);

            VaultEntryEntity? first = null;
            VaultEntryEntity? second = null;
            _fixture.VaultRepository
                .Setup(r => r.UpsertEntryAsync(It.IsAny<VaultEntryEntity>()))
                .Callback<VaultEntryEntity>(e =>
                {
                    if (first == null)
                        first = e;
                    else
                        second = e;
                })
                .Returns(Task.CompletedTask);

            var service = _fixture.CreateService();

            var a = await service.AddEntryAsync(new VaultEntry
            {
                WebsiteName = "A",
                Username = "u1",
                Password = "p1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            var b = await service.AddEntryAsync(new VaultEntry
            {
                WebsiteName = "B",
                Username = "u2",
                Password = "p2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            Assert.True(a.Success);
            Assert.True(b.Success);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first!.EncryptedData, second!.EncryptedData);
        }
    }
}
