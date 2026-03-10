using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Tests.Fixtures;
using PasswordManager.Tests.Helpers;

namespace PasswordManager.Tests.Services
{
    [Collection("Supabase")]
    public class AuthServiceIntegrationTests : IClassFixture<SupabaseFixture>, IAsyncLifetime
    {
        private readonly SupabaseFixture _fixture;

        public AuthServiceIntegrationTests(SupabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Leave the fixture in a neutral state after every test
            await _fixture.AuthService.LockAsync();
        }

        [Theory]
        [InlineData("", "Email cannot be empty.")]
        [InlineData("not-an-email", "Email must be a valid email address.")]
        [InlineData("missing@tld", "Email must be a valid email address.")]
        public async Task RegisterAsyncReturnsFailureWhenEmailIsInvalid(string email, string expectedMessage)
        {
            var result = await _fixture.AuthService.RegisterAsync(email, "ValidPassword1!");

            Assert.False(result.Success);
            Assert.Contains(expectedMessage, result.Message);
        }

        [Theory]
        [InlineData("", "Password cannot be empty.")]
        [InlineData("Ab1!", "Password must be at least 12 characters.")]
        [InlineData("abcdefghij1!", "Password must contain at least one uppercase letter.")]
        [InlineData("ABCDEFGHIJ1!", "Password must contain at least one lowercase letter.")]
        [InlineData("Abcdefghijkl!", "Password must contain at least one digit.")]
        [InlineData("Abcdefghijk1", "Password must contain at least one special character")]
        public async Task RegisterAsyncReturnsFailureWhenPasswordIsInvalid(string password, string expectedMessage)
        {
            var email = _fixture.GenerateUniqueEmail();

            var result = await _fixture.AuthService.RegisterAsync(email, password);

            Assert.False(result.Success);
            Assert.Contains(expectedMessage, result.Message);
        }

        [Fact]
        public async Task RegisterAsyncCreatesAuthUserAndProfileWithSaltAndVerificationToken()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var result = await _fixture.AuthService.RegisterAsync(email, password);

            // registration succeeded
            Assert.True(result.Success, $"RegisterAsync failed: {result.Message}");

            // confirm email so login can succeed
            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            // user exists in Supabase Auth, can log in with the same credentials
            var loginResult = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(loginResult.Success, $"LoginAsync failed: {loginResult.Message}");

            var userId = _fixture.SessionService.CurrentUserId;
            Assert.NotNull(userId);

            // UserProfiles row created by handle_new_user_profile trigger
            var profileResult = await _fixture.UserProfileService.GetProfileAsync(userId.Value);
            Assert.True(profileResult.Success, $"GetProfileAsync failed: {profileResult.Message}");

            var profile = profileResult.Value;

            // profile Id matches the auth user
            Assert.Equal(userId.Value, profile.Id);

            // salt is stored and is a valid 16-byte Argon2id salt
            Assert.False(string.IsNullOrWhiteSpace(profile.Salt), "Salt should not be empty");
            var saltBytes = Convert.FromBase64String(profile.Salt);
            Assert.Equal(16, saltBytes.Length);

            // encrypted verification token is stored and is a valid EncryptedBlob
            Assert.False(string.IsNullOrWhiteSpace(profile.EncryptedVerificationToken),
                "EncryptedVerificationToken should not be empty");

            var blobResult = EncryptedBlob.FromBase64String(profile.EncryptedVerificationToken);
            Assert.True(blobResult.Success, "Stored token is not a valid EncryptedBlob");

            // token round-trips: derive key then decrypt must succeed
            var derivedKey = _fixture.SessionService.GetDerivedKey();
            var decryptResult = _fixture.CryptoService.Decrypt(blobResult.Value, derivedKey);
            Assert.True(decryptResult.Success, "Verification token decryption failed — key mismatch");
        }

        [Fact]
        public async Task RegisterAsyncWithExistingEmailReturnsFailure()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var firstResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(firstResult.Success, $"First RegisterAsync failed: {firstResult.Message}");

            var secondResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.False(secondResult.Success, "Second RegisterAsync with the same email should have failed");
        }

        [Fact]
        public async Task LoginAsyncSetsSessionStateAndDerivedKey()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");

            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            var loginResult = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(loginResult.Success, $"LoginAsync failed: {loginResult.Message}");

            Assert.NotNull(_fixture.AuthService.CurrentUserId);
            Assert.Equal(email, _fixture.AuthService.CurrentUserEmail);

            Assert.True(_fixture.SessionService.IsActive(), "Session should be active after login");

            // derived key is a 32-byte AES-256 key
            var derivedKey = _fixture.SessionService.GetDerivedKey();
            Assert.NotNull(derivedKey);
            Assert.Equal(32, derivedKey.Length);
        }

        [Fact]
        public async Task LoginAsyncReturnsFailureWhenUsingWrongMasterPassword()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");

            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            var loginResult = await _fixture.AuthService
                .LoginAsync(email, string.Concat(password.Reverse()));
            Assert.False(loginResult.Success, $"LoginAsync should fail with invalid master password");
            Assert.Equal("Invalid email or password.", loginResult.Message);

            Assert.Null(_fixture.SessionService.CurrentUserId);
            Assert.Null(_fixture.SessionService.CurrentUserEmail);
            Assert.False(_fixture.SessionService.IsActive(), "Session should not be active after failed login");
        }

        [Fact]
        public async Task LoginAsyncReturnsFailureWhenUsingNonExistingUser()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var loginResult = await _fixture.AuthService
                .LoginAsync(email, password);
            Assert.False(loginResult.Success, "LoginAsync should fail for non-existent user");
            Assert.Equal("Invalid email or password.", loginResult.Message);

            Assert.Null(_fixture.SessionService.CurrentUserId);
            Assert.Null(_fixture.SessionService.CurrentUserEmail);
            Assert.False(_fixture.SessionService.IsActive(), "Session should not be active after failed login");
        }

        [Fact]
        public async Task LoginAsyncReturnsFailureWhenProfileRowIsMissing()
        {
            // register normally, then delete the profile row to simulate a trigger failure or manual data corruption
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");

            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            // Log in once to get the userId, then lock
            var firstLogin = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(firstLogin.Success, $"First LoginAsync failed: {firstLogin.Message}");
            var userId = _fixture.SessionService.CurrentUserId!.Value;
            await _fixture.AuthService.LockAsync();

            // Delete the profile row using an admin client so RLS cannot block the delete,
            // simulating a missing profile while the auth user still exists.
            if (_fixture.AdminSupabaseClient is null)
            {
                throw new InvalidOperationException(
                    "AdminSupabaseClient is not configured. Set Supabase__ServiceRoleKey (or equivalent) to run this integration test.");
            }

            await _fixture.AdminSupabaseClient
                .From<PasswordManager.Core.Entities.UserProfileEntity>()
                .Where(p => p.Id == userId)
                .Delete();

            var loginResult = await _fixture.AuthService.LoginAsync(email, password);

            Assert.False(loginResult.Success);
            Assert.Equal("User profile not found.", loginResult.Message);
        }

        [Fact]
        public async Task LockAsyncClearsSessionAndIsLockedReturnsTrue()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");
            Assert.True(_fixture.AuthService.IsLocked(), "New session should start in locked state");

            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            var loginResult = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(loginResult.Success, $"LoginAsync failed: {loginResult.Message}");
            Assert.False(_fixture.AuthService.IsLocked(), "Session should be unlocked after successful login");

            await _fixture.AuthService.LockAsync();

            Assert.True(_fixture.AuthService.IsLocked(), "IsLocked should return true after LockAsync");
            Assert.False(_fixture.SessionService.IsActive(), "Session should be inactive after LockAsync");
            Assert.Null(_fixture.SessionService.CurrentUserId);
            Assert.Null(_fixture.SessionService.CurrentUserEmail);
        }

        [Fact]
        public async Task RegisterAsyncSendsOtpEmailWith8DigitToken()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");
            
            Thread.Sleep(1000);

            var otpEmailResult = await InbucketClient.GetLatestOtpAsync(email);
            
            Assert.NotNull(otpEmailResult);
            Assert.Equal(8, otpEmailResult.Length);
            Assert.Matches("[0-9]{8}", otpEmailResult);
        }
        
        [Fact]
        public async Task VerifyEmailConfirmationAsyncReturnsOkWhenTokenIsValid()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");
            
            Thread.Sleep(1000);

            var otpEmailResult = await InbucketClient.GetLatestOtpAsync(email);
            Assert.NotNull(otpEmailResult);

            var emailConfirmationResult =
                await _fixture.AuthService.VerifyEmailConfirmationAsync(email, otpEmailResult);
            Assert.True(emailConfirmationResult.Success, $"VerifyEmailConfirmationAsync failed: {emailConfirmationResult.Message}");
        }
        
        [Fact]
        public async Task VerifyEmailConfirmationAsyncReturnsFailureWhenTokenIsInvalid()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");
            
            var randomToken = Random.Shared.Next(10000000, 99999999).ToString();

            var emailConfirmationResult =
                await _fixture.AuthService.VerifyEmailConfirmationAsync(email, randomToken);
            Assert.False(emailConfirmationResult.Success, $"VerifyEmailConfirmationAsync must fail with invalid token.");
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, emailConfirmationResult.Message);
        }
        
        [Fact]
        public async Task VerifyEmailConfirmationAsyncReturnsFailureWhenTokenIsExpired()
        {
            var email = _fixture.GenerateUniqueEmail();
            var password = "IntegrationTest1!";

            var registrationResult = await _fixture.AuthService.RegisterAsync(email, password);
            Assert.True(registrationResult.Success, $"RegisterAsync failed: {registrationResult.Message}");

            var otp = await InbucketClient.GetLatestOtpAsync(email);
            Assert.NotNull(otp);
            
            Thread.Sleep(8000);

            var emailConfirmationResult =
                await _fixture.AuthService.VerifyEmailConfirmationAsync(email, otp);
            Assert.False(emailConfirmationResult.Success,
                "Verification should fail with expired OTP. Ensure supabase/config.toml has otp_expiry = 5 and run: supabase stop && supabase start");
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, emailConfirmationResult.Message);
        }

    }
}