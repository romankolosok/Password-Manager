using Moq;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures;

namespace PasswordManager.Tests.Services
{
    public class VaultServiceTests : IClassFixture<VaultServiceFixture>
    {
        private readonly VaultServiceFixture _fixture;

        public VaultServiceTests(VaultServiceFixture fixture)
        {
            _fixture = fixture;
        }

        // Builds the JSON string that VaultEntryPayload.FromJson() expects.
        private static string MakePayloadJson(
            string? site = null,
            string? user = null,
            string? pass = null) =>
            $$"""{"WebsiteName":"{{site ?? TestData.WebsiteName()}}","Username":"{{user ?? TestData.Username()}}","Password":"{{pass ?? TestData.Password()}}","Url":"","Notes":"","Category":"","IsFavorite":false}""";

        // Builds a valid entity and wires Decrypt().
        private VaultEntryEntity MakeEntity(Guid userId, string payloadJson) =>
            _fixture.BuildEncryptedEntity(userId, payloadJson);

        [Fact]
        public async Task GetAllEntriesAsyncReturnsFailureWhenSessionIsInactive()
        {
            _fixture.Reset();

            _fixture.SetupInactiveSession();

            var result = await _fixture.CreateService().GetAllEntriesAsync();

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsEmptyListWhenRepositoryHasNoEntries()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            _fixture.VaultRepository
                .Setup(repo => repo.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>());

            var result = await _fixture.CreateService().GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsDecryptedEntryWhenOneEntityDecryptsSuccessfully()
        {
            _fixture.Reset();

            var userId = TestData.UserId();

            var site = TestData.WebsiteName();
            var user = TestData.Username();
            var pass = TestData.Password();

            _fixture.SetupActiveSession(userId);

            var entity = MakeEntity(userId, MakePayloadJson(site, user, pass));
            _fixture.VaultRepository
                .Setup(repo => repo.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity> { entity });

            var result = await _fixture.CreateService().GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Single(result.Value!);

            var entry = result.Value![0];
            Assert.Equal(site, entry.WebsiteName);
            Assert.Equal(user, entry.Username);
            Assert.Equal(pass, entry.Password);
            Assert.Equal(entity.Id, entry.Id);
            Assert.Equal(entity.CreatedAt, entry.CreatedAt);
        }

        [Fact]
        public async Task GetAllEntriesAsyncSkipsEntryWhenDecryptionFails()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            // Decrypt() returns Fail — simulates ciphertext corruption or key mismatch.
            // The entity is built manually so we do not overwrite this setup via MakeEntity().
            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Fail("decryption failed"));

            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>
                {
                    new() {
                        Id = TestData.UserId(),
                        UserId = userId,
                        EncryptedData = blob.ToBase64String(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                });

            var service = _fixture.CreateService();
            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task GetAllEntriesAsyncSkipsEntryWhenEncryptedDataIsCorrupt()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            // Invalid base64 causes EncryptedBlob.FromBase64String() to return Fail
            // TryDecryptEntry() exits before calling Decrypt() at all.
            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>
                {
                    new() {
                        Id = TestData.UserId(),
                        UserId = userId,
                        EncryptedData = "!!!not-valid-base64!!!",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                });

            var service = _fixture.CreateService();
            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task GetAllEntriesAsyncSkipsEntryWhenDecryptedJsonIsInvalid()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            // Decrypt() succeeds but produces a string that VaultEntryPayload.FromJson()
            // cannot parse — simulates entries written in a legacy or incompatible format.
            _fixture.CryptoService
                .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                .Returns(Result<string>.Ok("{ json"));

            var blob = new EncryptedBlob
            {
                Nonce = new byte[12],
                Ciphertext = new byte[1],
                Tag = new byte[16]
            };

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>
                {
                    new() {
                        Id = TestData.UserId(),
                        UserId = userId,
                        EncryptedData = blob.ToBase64String(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                });

            var service = _fixture.CreateService();
            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsMixedResultsWhenSomeEntriesDecryptAndSomeFail()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var site = TestData.WebsiteName();
            _fixture.SetupActiveSession(userId);

            // Good entity: MakeEntity() wires Decrypt() to return parseable JSON.
            var goodEntity = MakeEntity(userId, MakePayloadJson(site));

            // Bad entity: corrupt base64 fails at blob-parse, before Decrypt() is called
            // so the Decrypt() setup wired above is not disturbed.
            var badEntity = new VaultEntryEntity
            {
                Id = TestData.UserId(),
                UserId = userId,
                EncryptedData = "!!!not-valid-base64!!!",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity> { goodEntity, badEntity });

            var service = _fixture.CreateService();
            var result = await service.GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Single(result.Value!);
            Assert.Equal(site, result.Value![0].WebsiteName);
        }
    }
}