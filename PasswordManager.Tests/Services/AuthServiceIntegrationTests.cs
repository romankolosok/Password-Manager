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

            Assert.True(result.Success, $"RegisterAsync failed: {result.Message}");

            var confirmResult = await _fixture.ConfirmEmailAsync(email);
            Assert.True(confirmResult.Success, $"VerifyEmailConfirmationAsync failed: {confirmResult.Message}");

            // user exists in Supabase Auth
            var loginResult = await _fixture.AuthService.LoginAsync(email, password);
            Assert.True(loginResult.Success, $"LoginAsync failed: {loginResult.Message}");

            var userId = _fixture.SessionService.CurrentUserId;
            Assert.NotNull(userId);

            // UserProfiles row created by handle_new_user_profile trigger
            var profileResult = await _fixture.UserProfileService.GetProfileAsync(userId.Value);
            Assert.True(profileResult.Success, $"GetProfileAsync failed: {profileResult.Message}");

            var profile = profileResult.Value;
            Assert.Equal(userId.Value, profile.Id);

            // valid 16-byte Argon2id salt
            Assert.False(string.IsNullOrWhiteSpace(profile.Salt), "Salt should not be empty");
            var saltBytes = Convert.FromBase64String(profile.Salt);
            Assert.Equal(16, saltBytes.Length);

            // EncryptedDEK is stored and is a valid EncryptedBlob (DEK model)
            Assert.False(string.IsNullOrWhiteSpace(profile.EncryptedDEK),
                "EncryptedDEK should not be empty — new accounts must use the DEK model");

            var dekBlobResult = EncryptedBlob.FromBase64String(profile.EncryptedDEK);
            Assert.True(dekBlobResult.Success, "Stored EncryptedDEK is not a valid EncryptedBlob");

            // Derive KEK from password + stored salt, then decrypt EncryptedDEK
            var kek = _fixture.CryptoService.DeriveKey(password, saltBytes);
            var dekDecryptResult = _fixture.CryptoService.Decrypt(dekBlobResult.Value, kek);
            Assert.True(dekDecryptResult.Success, "DEK decryption failed — KEK mismatch");

            var dekBytes = Convert.FromBase64String(dekDecryptResult.Value);
            Assert.Equal(32, dekBytes.Length);

            // Session key must be the decrypted DEK (not the KEK)
            var sessionKey = _fixture.SessionService.GetDerivedKey();
            Assert.NotNull(sessionKey);
            Assert.Equal(32, sessionKey.Length);
            Assert.Equal(dekBytes, sessionKey);
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

        // ── Change Master Password ────────────────────────────────────────────────

        [Fact]
        public async Task ChangeMasterPasswordAsyncSucceedsAndAllowsLoginWithNewPassword()
        {
            const string originalPassword = "IntegrationTest1!";
            const string newPassword = "IntegrationTest2!";

            var setupResult = await _fixture.RegisterConfirmAndLoginAsync(originalPassword);
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;

            var changeResult = await _fixture.AuthService.ChangeMasterPasswordAsync(originalPassword, newPassword);
            Assert.True(changeResult.Success, $"ChangeMasterPasswordAsync failed: {changeResult.Message}");

            // Capture the DEK that was set after the password change
            var dekAfterChange = _fixture.SessionService.GetDerivedKey();
            Assert.NotNull(dekAfterChange);
            Assert.Equal(32, dekAfterChange.Length);

            // Lock and re-login with new password — must succeed
            await _fixture.AuthService.LockAsync();
            var loginResult = await _fixture.AuthService.LoginAsync(email, newPassword);
            Assert.True(loginResult.Success, $"LoginAsync with new password failed: {loginResult.Message}");

            // DEK after re-login must equal DEK after change (same underlying vault key)
            var dekAfterRelogin = _fixture.SessionService.GetDerivedKey();
            Assert.Equal(dekAfterChange, dekAfterRelogin);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncFailsWithWrongCurrentPassword()
        {
            const string password = "IntegrationTest1!";

            var setupResult = await _fixture.RegisterConfirmAndLoginAsync(password);
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");

            var changeResult = await _fixture.AuthService
                .ChangeMasterPasswordAsync("IntegrationTest2!", password);

            Assert.False(changeResult.Success, "ChangeMasterPasswordAsync should fail with wrong current password");
            Assert.Equal("Invalid email or password.", changeResult.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncFailsWhenNotLoggedIn()
        {
            // session is empty
            var changeResult = await _fixture.AuthService
                .ChangeMasterPasswordAsync("IntegrationTest1!", "IntegrationTest2!");

            Assert.False(changeResult.Success);
            Assert.Equal("Not logged in.", changeResult.Message);
        }

        [Fact]
        public async Task ChangeMasterPasswordAsyncOriginalPasswordNoLongerWorksAfterChange()
        {
            const string originalPassword = "IntegrationTest1!";
            const string newPassword = "IntegrationTest2!";

            var setupResult = await _fixture.RegisterConfirmAndLoginAsync(originalPassword);
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;

            var changeResult = await _fixture.AuthService.ChangeMasterPasswordAsync(originalPassword, newPassword);
            Assert.True(changeResult.Success, $"ChangeMasterPasswordAsync failed: {changeResult.Message}");

            await _fixture.AuthService.LockAsync();

            // Original password must no longer authenticate
            var loginWithOldResult = await _fixture.AuthService.LoginAsync(email, originalPassword);
            Assert.False(loginWithOldResult.Success, "Login with old password must fail after master password change");
        }


        [Fact]
        public async Task SetupRecoveryKeyAsyncReturnsValidKeyAndPersistsRecoveryFields()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var userId = setupResult.Value.UserId;

            var recoveryResult = await _fixture.AuthService.SetupRecoveryKeyAsync();
            Assert.True(recoveryResult.Success, $"SetupRecoveryKeyAsync failed: {recoveryResult.Message}");

            var recoveryKey = recoveryResult.Value;
            Assert.NotNull(recoveryKey);

            // Recovery key must be valid base64 encoding a 32-byte value
            var recoveryKeyBytes = Convert.FromBase64String(recoveryKey);
            Assert.Equal(32, recoveryKeyBytes.Length);

            // Profile must have RecoveryEncryptedDEK and RecoverySalt persisted
            var profileResult = await _fixture.UserProfileService.GetProfileAsync(userId);
            Assert.True(profileResult.Success, $"GetProfileAsync failed: {profileResult.Message}");

            var profile = profileResult.Value;
            Assert.False(string.IsNullOrWhiteSpace(profile.RecoveryEncryptedDEK),
                "RecoveryEncryptedDEK should be stored after setup");
            Assert.False(string.IsNullOrWhiteSpace(profile.RecoverySalt),
                "RecoverySalt should be stored after setup");

            // RecoveryEncryptedDEK must be a valid EncryptedBlob
            var recBlobResult = EncryptedBlob.FromBase64String(profile.RecoveryEncryptedDEK!);
            Assert.True(recBlobResult.Success, "RecoveryEncryptedDEK is not a valid EncryptedBlob");

            // Deriving recovery KEK from the returned key + stored salt must decrypt the DEK
            var recoverySalt = Convert.FromBase64String(profile.RecoverySalt!);
            var recoveryKek = _fixture.CryptoService.DeriveKey(recoveryKey, recoverySalt);
            var dekDecrypt = _fixture.CryptoService.Decrypt(recBlobResult.Value, recoveryKek);
            Assert.True(dekDecrypt.Success, "Could not decrypt RecoveryEncryptedDEK with returned recovery key");

            var recoveredDekBytes = Convert.FromBase64String(dekDecrypt.Value);
            Assert.Equal(32, recoveredDekBytes.Length);

            // Decrypted DEK must equal the current session key
            var sessionKey = _fixture.SessionService.GetDerivedKey();
            Assert.Equal(sessionKey, recoveredDekBytes);
        }

        [Fact]
        public async Task SetupRecoveryKeyAsyncFailsWhenNotLoggedIn()
        {
            var result = await _fixture.AuthService.SetupRecoveryKeyAsync();

            Assert.False(result.Success);
            Assert.Equal("Not logged in.", result.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncSucceedsAndAllowsLoginWithNewPassword()
        {
            const string originalPassword = "IntegrationTest1!";
            const string newPassword = "IntegrationTest2!";

            // Register, confirm, login, then set up recovery key
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync(originalPassword);
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;

            var recoverySetupResult = await _fixture.AuthService.SetupRecoveryKeyAsync();
            Assert.True(recoverySetupResult.Success, $"SetupRecoveryKeyAsync failed: {recoverySetupResult.Message}");
            var recoveryKey = recoverySetupResult.Value;

            // Capture DEK before recovery
            var dekBeforeRecovery = _fixture.SessionService.GetDerivedKey();

            // Perform vault recovery (session still active — simulates post-OTP state)
            var recoverResult = await _fixture.AuthService.RecoverVaultAsync(recoveryKey, newPassword);
            Assert.True(recoverResult.Success, $"RecoverVaultAsync failed: {recoverResult.Message}");

            // Session key must still be the same DEK (only wrapping changed)
            var dekAfterRecovery = _fixture.SessionService.GetDerivedKey();
            Assert.Equal(dekBeforeRecovery, dekAfterRecovery);

            // Lock and re-login with new password — must succeed
            await _fixture.AuthService.LockAsync();
            var loginResult = await _fixture.AuthService.LoginAsync(email, newPassword);
            Assert.True(loginResult.Success, $"LoginAsync with recovered password failed: {loginResult.Message}");

            var dekAfterRelogin = _fixture.SessionService.GetDerivedKey();
            Assert.Equal(dekBeforeRecovery, dekAfterRelogin);
        }

        [Fact]
        public async Task RecoverVaultAsyncFailsWithWrongRecoveryKey()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");

            var recoverySetupResult = await _fixture.AuthService.SetupRecoveryKeyAsync();
            Assert.True(recoverySetupResult.Success, $"SetupRecoveryKeyAsync failed: {recoverySetupResult.Message}");

            // Use a different random 32-byte key, encoded to base64
            var wrongKey = Convert.ToBase64String(new byte[32]); // all-zeros key — wrong
            var recoverResult = await _fixture.AuthService.RecoverVaultAsync(wrongKey, "IntegrationTest2!");

            Assert.False(recoverResult.Success, "RecoverVaultAsync should fail with wrong recovery key");
            Assert.Equal("Invalid recovery key.", recoverResult.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncFailsWhenNoRecoveryKeySetUp()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");

            // Do NOT call SetupRecoveryKeyAsync
            var recoverResult = await _fixture.AuthService
                .RecoverVaultAsync("someKey", "IntegrationTest1!");

            Assert.False(recoverResult.Success);
            Assert.Equal("No recovery key has been set up for this account.", recoverResult.Message);
        }

        [Fact]
        public async Task RecoverVaultAsyncFailsWhenNotLoggedIn()
        {
            var result = await _fixture.AuthService
                .RecoverVaultAsync("someKey", "IntegrationTest1!");

            Assert.False(result.Success);
            Assert.Equal("Not logged in.", result.Message);
        }

        [Fact]
        public async Task SendResetPasswordEmailAsyncSendsEmailWithOtp()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;
            await _fixture.AuthService.LockAsync();

            var sendResult = await _fixture.AuthService.SendResetPasswordEmailAsync(email);
            Assert.True(sendResult.Success, $"SendResetPasswordEmailAsync failed: {sendResult.Message}");

            Thread.Sleep(1000);

            var otp = await InbucketClient.GetLatestOtpAsync(email);
            Assert.NotNull(otp);
            Assert.Equal(8, otp.Length);
            Assert.Matches("[0-9]{8}", otp);
        }

        [Fact]
        public async Task VerifyPasswordResetAsyncSucceedsWithValidOtp()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;
            await _fixture.AuthService.LockAsync();

            var sendResult = await _fixture.AuthService.SendResetPasswordEmailAsync(email);
            Assert.True(sendResult.Success, $"SendResetPasswordEmailAsync failed: {sendResult.Message}");

            Thread.Sleep(1000);

            var otp = await InbucketClient.GetLatestOtpAsync(email);
            Assert.NotNull(otp);

            var verifyResult = await _fixture.AuthService.VerifyPasswordResetAsync(email, otp);
            Assert.True(verifyResult.Success, $"VerifyPasswordResetAsync failed: {verifyResult.Message}");
        }

        [Fact]
        public async Task VerifyPasswordResetAsyncFailsWithInvalidOtp()
        {
            var setupResult = await _fixture.RegisterConfirmAndLoginAsync("IntegrationTest1!");
            Assert.True(setupResult.Success, $"Setup failed: {setupResult.Message}");
            var email = setupResult.Value.Email;
            await _fixture.AuthService.LockAsync();

            var sendResult = await _fixture.AuthService.SendResetPasswordEmailAsync(email);
            Assert.True(sendResult.Success, $"SendResetPasswordEmailAsync failed: {sendResult.Message}");

            var randomToken = Random.Shared.Next(10000000, 99999999).ToString();
            var verifyResult = await _fixture.AuthService.VerifyPasswordResetAsync(email, randomToken);

            Assert.False(verifyResult.Success, "VerifyPasswordResetAsync must fail with invalid OTP");
            Assert.Equal(AuthMessages.OtpInvalidOrExpired, verifyResult.Message);
        }

    }
}