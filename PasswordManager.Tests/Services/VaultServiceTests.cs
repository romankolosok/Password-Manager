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

        // Builds a minimal valid blob entity without touching the Decrypt() mock.
        // Use this when the test controls Decrypt() itself.
        private static VaultEntryEntity MakeBlobEntity(Guid userId) =>
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] }.ToBase64String(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        public static IEnumerable<object[]> DecryptionFailureCases()
        {
            // Corrupt base64 — EncryptedBlob.FromBase64String() returns Fail
            yield return new object[] { "!!!not-valid-base64!!!", null! };
            // Valid blob, Decrypt() returns Fail — key mismatch / ciphertext corrupt
            yield return new object[] { null!, Result<string>.Fail("decryption failed") };
            // Valid blob, Decrypt() returns Ok but JSON is unparseable
            yield return new object[] { null!, Result<string>.Ok("{ json") };
        }

        [Fact]
        public async Task GetAllEntriesAsyncReturnsFailureWhenSessionIsInactive()
        {
            _fixture.Reset();
            _fixture.SetupInactiveSession();

            var result = await _fixture
                .CreateService()
                .GetAllEntriesAsync();

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

            var result = await _fixture
                .CreateService()
                .GetAllEntriesAsync();

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

            var result = await _fixture
                .CreateService()
                .GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Single(result.Value!);

            var entry = result.Value![0];
            Assert.Equal(site, entry.WebsiteName);
            Assert.Equal(user, entry.Username);
            Assert.Equal(pass, entry.Password);
            Assert.Equal(entity.Id, entry.Id);
            Assert.Equal(entity.CreatedAt, entry.CreatedAt);
        }

        [Theory]
        [MemberData(nameof(DecryptionFailureCases))]
        public async Task GetAllEntriesAsyncSkipsEntryWhenTryDecryptEntryFails(
                    string? corruptBase64,
                    Result<string>? decryptResult)
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            if (decryptResult != null)
            {
                _fixture.CryptoService
                    .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                    .Returns(decryptResult);
            }

            var encryptedData = corruptBase64
                ?? new EncryptedBlob { Nonce = new byte[12], Ciphertext = new byte[1], Tag = new byte[16] }.ToBase64String();

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity>
                {
                    new() { Id = Guid.NewGuid(), UserId = userId, EncryptedData = encryptedData, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                });

            var result = await _fixture.CreateService().GetAllEntriesAsync();

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
                Id = Guid.NewGuid(),
                UserId = userId,
                EncryptedData = "!!!not-valid-base64!!!",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _fixture.VaultRepository
                .Setup(r => r.GetAllEntriesAsync(userId))
                .ReturnsAsync(new List<VaultEntryEntity> { goodEntity, badEntity });

            var result = await _fixture
                .CreateService()
                .GetAllEntriesAsync();

            Assert.True(result.Success);
            Assert.Single(result.Value!);
            Assert.Equal(site, result.Value![0].WebsiteName);
        }


        [Fact]
        public async Task GetEntryAsyncReturnsFailureWhenSessionIsInactive()
        {
            _fixture.Reset();
            _fixture.SetupInactiveSession();

            var result = await _fixture
                .CreateService()
                .GetEntryAsync(Guid.NewGuid().ToString());

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsFailureWhenGuidIsInvalid()
        {
            _fixture.Reset();
            _fixture.SetupActiveSession();

            var result = await _fixture
                .CreateService()
                .GetEntryAsync("!!!not-a-valid-guid!!!");

            Assert.False(result.Success);
            Assert.Equal("Invalid entry id", result.Message);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsFailureWhenRepositoryReturnsNull()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var entryId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);

            _fixture.VaultRepository
                .Setup(r => r.GetEntryAsync(userId, entryId))
                .ReturnsAsync((VaultEntryEntity?)null);

            var result = await _fixture
                .CreateService()
                .GetEntryAsync(entryId.ToString());

            Assert.False(result.Success);
            Assert.Equal("Entry not found", result.Message);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsDecryptedEntryWhenEntityExistsAndDecryptsSuccessfully()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var site = TestData.WebsiteName();
            var user = TestData.Username();
            var pass = TestData.Password();

            _fixture.SetupActiveSession(userId);

            var entity = MakeEntity(userId, MakePayloadJson(site, user, pass));
            _fixture.VaultRepository
                .Setup(r => r.GetEntryAsync(userId, entity.Id))
                .ReturnsAsync(entity);

            var result = await _fixture
                .CreateService()
                .GetEntryAsync(entity.Id.ToString());

            Assert.True(result.Success);
            var entry = result.Value!;
            Assert.Equal(site, entry.WebsiteName);
            Assert.Equal(user, entry.Username);
            Assert.Equal(pass, entry.Password);
            Assert.Equal(entity.Id, entry.Id);
            Assert.Equal(entity.CreatedAt, entry.CreatedAt);
        }

        [Theory]
        [MemberData(nameof(DecryptionFailureCases))]
        public async Task GetEntryAsyncReturnsFailureWhenTryDecryptEntryFails(
            string? corruptBase64,
            Result<string>? decryptResult)
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var entryId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);

            if (decryptResult != null)
            {
                _fixture.CryptoService
                    .Setup(c => c.Decrypt(It.IsAny<EncryptedBlob>(), It.IsAny<byte[]>()))
                    .Returns(decryptResult);
            }

            var encryptedData = corruptBase64
                ?? (new EncryptedBlob
                {
                    Nonce = new byte[12],
                    Ciphertext = new byte[1],
                    Tag = new byte[16]
                }
                ).ToBase64String();

            _fixture.VaultRepository
                .Setup(r => r.GetEntryAsync(userId, entryId))
                .ReturnsAsync(new VaultEntryEntity
                {
                    Id = entryId,
                    UserId = userId,
                    EncryptedData = encryptedData,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            var result = await _fixture.CreateService().GetEntryAsync(entryId.ToString());

            Assert.False(result.Success);
            Assert.Equal("Failed to decrypt entry", result.Message);
        }

        [Fact]
        public async Task GetEntryAsyncReturnsFailureWhenRepositoryThrows()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var entryId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);

            _fixture.VaultRepository
                .Setup(r => r.GetEntryAsync(userId, entryId))
                .ThrowsAsync(new Exception("db unavailable"));

            var result = await _fixture.CreateService().GetEntryAsync(entryId.ToString());

            Assert.False(result.Success);
            Assert.Equal("Failed to retrieve entry", result.Message);
        }

        [Fact]
        public async Task AddEntryAsyncReturnsFailureWhenSessionIsInactive()
        {
            _fixture.Reset();
            _fixture.SetupInactiveSession();

            var entry = new VaultEntry
            {
                Id = Guid.NewGuid(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow,
            };

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }

        [Fact]
        public async Task AddEntryAsyncGeneratesIdAndTimestampsWhenEntryIsNew()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            // entry with empty Id and default CreatedAt signals a new entry
            var entry = new VaultEntry
            {
                Id = Guid.Empty,
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = default,
            };

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(new EncryptedBlob
                {
                    Nonce = new byte[12],
                    Ciphertext = new byte[1],
                    Tag = new byte[16]
                }));

            VaultEntryEntity? savedEntity = null;
            _fixture.VaultRepository
                .Setup(r => r.UpsertEntryAsync(It.IsAny<VaultEntryEntity>()))
                .Callback<VaultEntryEntity>(e => savedEntity = e)
                .Returns(Task.CompletedTask);

            var beforeCall = DateTime.UtcNow;

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            var afterCall = DateTime.UtcNow;

            Assert.True(result.Success);
            Assert.NotNull(savedEntity);
            Assert.NotEqual(Guid.Empty, savedEntity!.Id);

            Assert.InRange(savedEntity.CreatedAt, beforeCall, afterCall);
            Assert.InRange(savedEntity.UpdatedAt, beforeCall, afterCall);
        }

        [Theory]
        [InlineData("encryption failed", "encryption failed")]
        [InlineData(null, "Failed to encrypt entry")]
        public async Task AddEntryAsyncReturnsFailureWhenEncryptionFails(
            string? failMessage,
            string expectedMessage)
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            var entry = new VaultEntry
            {
                Id = Guid.NewGuid(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow,
            };

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail(failMessage!));

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            Assert.False(result.Success);
            Assert.Equal(expectedMessage, result.Message);
        }

        [Fact]
        public async Task AddEntryAsyncReturnsFailureWhenEncryptionFailsWithoutMessage()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            // entry with empty Id and default CreatedAt signals a new entry
            var entry = new VaultEntry
            {
                Id = Guid.NewGuid(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow,
            };

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Fail(null!));

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            Assert.False(result.Success);
            Assert.Equal("Failed to encrypt entry", result.Message);
        }

        [Fact]
        public async Task AddEntryAsyncReturnsFailureWhenRepositoryThrows()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            var entry = new VaultEntry
            {
                Id = Guid.NewGuid(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow,
            };

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(new EncryptedBlob
                {
                    Nonce = new byte[12],
                    Ciphertext = new byte[1],
                    Tag = new byte[16]
                }));

            _fixture.VaultRepository
                .Setup(r => r.UpsertEntryAsync(It.IsAny<VaultEntryEntity>()))
                .ThrowsAsync(new Exception("db unavailable"));

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            Assert.False(result.Success);
            Assert.Equal("Failed to save entry", result.Message);
        }

        [Fact]
        public async Task AddEntryAsyncReturnsOkWhenEntryIsSavedSuccessfully()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            var entry = new VaultEntry
            {
                Id = Guid.NewGuid(),
                WebsiteName = TestData.WebsiteName(),
                Username = TestData.Username(),
                Password = TestData.Password(),
                Url = TestData.Url(),
                Notes = TestData.Notes(),
                Category = TestData.Category(),
                IsFavorite = false,
                CreatedAt = DateTime.UtcNow,
            };

            _fixture.CryptoService
                .Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(Result<EncryptedBlob>.Ok(new EncryptedBlob
                {
                    Nonce = new byte[12],
                    Ciphertext = new byte[1],
                    Tag = new byte[16]
                }));

            _fixture.VaultRepository
                .Setup(r => r.UpsertEntryAsync(It.IsAny<VaultEntryEntity>()))
                .Returns(Task.CompletedTask);

            var result = await _fixture
                .CreateService()
                .AddEntryAsync(entry);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task DeleteEntryAsyncReturnsFailureWhenSessionIsInactive()
        {
            _fixture.Reset();
            _fixture.SetupInactiveSession();

            var result = await _fixture
                .CreateService()
                .DeleteEntryAsync(Guid.NewGuid().ToString());

            Assert.False(result.Success);
            Assert.Equal("Vault is locked", result.Message);
        }

        [Fact]
        public async Task DeleteEntryAsyncReturnsFailureWhenEntryIdIsInvalid()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            _fixture.SetupActiveSession(userId);

            var result = await _fixture
                .CreateService()
                .DeleteEntryAsync("!!not-valid-guid!!");

            Assert.False(result.Success);
            Assert.Equal("Invalid entry id", result.Message);
        }

        [Fact]
        public async Task DeleteEntryAsyncReturnsOkWhenEntryDeletedSuccessfully()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var entryId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);

            _fixture.VaultRepository
                .Setup(r => r.DeleteEntryAsync(userId, entryId))
                .Returns(Task.CompletedTask);

            var result = await _fixture
                .CreateService()
                .DeleteEntryAsync(entryId.ToString());

            Assert.True(result.Success);
        }

        [Fact]
        public async Task DeleteEntryAsyncReturnsFailureWhenRepositoryThrows()
        {
            _fixture.Reset();

            var userId = TestData.UserId();
            var entryId = Guid.NewGuid();
            _fixture.SetupActiveSession(userId);

            _fixture.VaultRepository
                .Setup(r => r.DeleteEntryAsync(userId, entryId))
                .ThrowsAsync(new Exception("db unavailable"));

            var result = await _fixture
                .CreateService()
                .DeleteEntryAsync(entryId.ToString());

            Assert.False(result.Success);
            Assert.Equal("Failed to delete entry", result.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SearchEntriesReturnsAllEntriesWhenQueryIsNullOrWhitespace(string? query)
        {
            var entries = new List<VaultEntry>
            {
                new() { WebsiteName = "GitHub",  Username = "alice" },
                new() { WebsiteName = "Google",  Username = "bob"   },
            };

            var result = _fixture.CreateService().SearchEntries(query!, entries);

            Assert.Equal(entries, result);
        }

        [Fact]
        public void SearchEntriesReturnsMatchingEntryWhenQueryMatchesWebsiteName()
        {
            var match = new VaultEntry { WebsiteName = "GitHub" };
            var noMatch = new VaultEntry { WebsiteName = "Google" };

            var result = _fixture.CreateService().SearchEntries("github", new List<VaultEntry> { match, noMatch });

            Assert.Single(result);
            Assert.Equal(match, result[0]);
        }

        [Fact]
        public void SearchEntriesReturnsMatchingEntryWhenQueryMatchesUsername()
        {
            var match = new VaultEntry { Username = "alice" };
            var noMatch = new VaultEntry { Username = "bob" };

            var result = _fixture.CreateService().SearchEntries("alice", new List<VaultEntry> { match, noMatch });

            Assert.Single(result);
            Assert.Equal(match, result[0]);
        }

        [Fact]
        public void SearchEntriesReturnsMatchingEntryWhenQueryMatchesUrl()
        {
            var match = new VaultEntry { Url = "https://github.com" };
            var noMatch = new VaultEntry { Url = "https://google.com" };

            var result = _fixture.CreateService().SearchEntries("github", new List<VaultEntry> { match, noMatch });

            Assert.Single(result);
            Assert.Equal(match, result[0]);
        }

        [Fact]
        public void SearchEntriesReturnsMatchingEntryWhenQueryMatchesNotes()
        {
            var match = new VaultEntry { Notes = "work account" };
            var noMatch = new VaultEntry { Notes = "personal account" };

            var result = _fixture.CreateService().SearchEntries("work", new List<VaultEntry> { match, noMatch });

            Assert.Single(result);
            Assert.Equal(match, result[0]);
        }

        [Fact]
        public void SearchEntriesReturnsMatchingEntryWhenQueryMatchesCategory()
        {
            var match = new VaultEntry { Category = "Finance" };
            var noMatch = new VaultEntry { Category = "Social" };

            var result = _fixture.CreateService().SearchEntries("finance", new List<VaultEntry> { match, noMatch });

            Assert.Single(result);
            Assert.Equal(match, result[0]);
        }

        [Fact]
        public void SearchEntriesReturnsEmptyListWhenNoEntriesMatch()
        {
            var entries = new List<VaultEntry>
            {
                new() { WebsiteName = "GitHub",  Username = "alice" },
                new() { WebsiteName = "Google",  Username = "bob"   },
            };

            var result = _fixture.CreateService().SearchEntries("nomatch", entries);

            Assert.Empty(result);
        }

        [Fact]
        public void SearchEntriesMatchesCaseInsensitively()
        {
            var entry = new VaultEntry { WebsiteName = "GitHub" };

            var result = _fixture.CreateService().SearchEntries("GITHUB", new List<VaultEntry> { entry });

            Assert.Single(result);
            Assert.Equal(entry, result[0]);
        }

        [Fact]
        public void SearchEntriesIgnoresLeadingAndTrailingWhitespaceInQuery()
        {
            var entry = new VaultEntry { WebsiteName = "GitHub" };

            var result = _fixture.CreateService().SearchEntries("  github  ", new List<VaultEntry> { entry });

            Assert.Single(result);
            Assert.Equal(entry, result[0]);
        }
    }
}