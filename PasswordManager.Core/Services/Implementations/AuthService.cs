using Microsoft.Extensions.Logging;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using Supabase.Gotrue;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly Supabase.Client _supabase;
        private readonly ICryptoService _cryptoService;
        private readonly IUserProfileService _userProfileService;
        private readonly IVaultRepository _vaultRepository;
        private readonly ISessionService _sessionService;
        private readonly ISupabaseExceptionMapper _exceptionMapper;
        private readonly ILogger<AuthService> _logger;
        private readonly PasswordValidator _passwordValidator = new();
        private readonly EmailValidator _emailValidator = new();

        public Guid? CurrentUserId => _sessionService.CurrentUserId;
        public string? CurrentUserEmail => _sessionService.CurrentUserEmail;

        public AuthService(
            Supabase.Client supabase,
            ICryptoService cryptoService,
            IUserProfileService userProfileService,
            IVaultRepository vaultRepository,
            ISessionService sessionService,
            ISupabaseExceptionMapper exceptionMapper,
            ILogger<AuthService> logger)
        {
            _supabase = supabase;
            _cryptoService = cryptoService;
            _userProfileService = userProfileService;
            _vaultRepository = vaultRepository;
            _sessionService = sessionService;
            _exceptionMapper = exceptionMapper;
            _logger = logger;

            _supabase.Auth.AddStateChangedListener(OnAuthStateChanged);
        }

        [ExcludeFromCodeCoverage]
        private void OnAuthStateChanged(object? sender, Supabase.Gotrue.Constants.AuthState state)
        {
            switch (state)
            {
                case Supabase.Gotrue.Constants.AuthState.SignedOut:
                    _logger.LogInformation("Auth state changed: SignedOut");
                    _sessionService.ClearSession();
                    break;
                case Supabase.Gotrue.Constants.AuthState.TokenRefreshed:
                    _logger.LogInformation("Auth state changed: TokenRefreshed");
                    // Update access token in session
                    var session = _supabase.Auth.CurrentSession;
                    if (session?.AccessToken != null && _sessionService.CurrentUserId != null)
                    {
                        _sessionService.SetUser(
                            _sessionService.CurrentUserId.Value,
                            _sessionService.CurrentUserEmail ?? session.User?.Email ?? "",
                            session.AccessToken
                        );
                    }
                    break;
                case Supabase.Gotrue.Constants.AuthState.SignedIn:
                    _logger.LogInformation("Auth state changed: SignedIn");
                    break;
                case Supabase.Gotrue.Constants.AuthState.UserUpdated:
                    _logger.LogInformation("Auth state changed: UserUpdated");
                    break;
            }
        }

        public async Task<Result> RegisterAsync(string email, string masterPassword)
        {
            // Validate inputs
            var validationResult = ValidateCredentials(email, masterPassword);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            var (salt, kek) = CreateCryptographicMaterials(masterPassword);

            // Generate a random DEK and encrypt it with the password-derived KEK
            var dekBytes = new byte[32];
            RandomNumberGenerator.Fill(dekBytes);
            var dekBase64 = Convert.ToBase64String(dekBytes);
            var encryptedDekResult = _cryptoService.Encrypt(dekBase64, kek);
            if (!encryptedDekResult.Success)
                return Result.Fail("Failed to create account. Please try again.");

            var signUpMetadata = new Dictionary<string, object>
            {
                { "salt", Convert.ToBase64String(salt) },
                { "encrypted_dek", encryptedDekResult.Value.ToBase64String() }
            };

            var sessionResult = await CreateAuthSessionAsync(email, masterPassword, signUpMetadata);
            if (!sessionResult.Success)
                return sessionResult;

            var session = sessionResult.Value;

            var signUpValidation = ValidateSignUpSession(session, email);
            if (signUpValidation != null)
                return signUpValidation;

            var userIdStr = session!.User!.Id;
            if (string.IsNullOrEmpty(userIdStr))
                return Result.Fail("Invalid user ID.");
            var authUserId = Guid.Parse(userIdStr);
            await _supabase.Auth.SignOut();
            _logger.LogInformation("User {UserId} registered successfully; redirecting to login", authUserId);
            return Result.Ok();
        }

        public async Task<Result> LoginAsync(string email, string masterPassword)
        {
            // Authenticate with Supabase
            var sessionResult = await AuthenticateAsync(email, masterPassword);
            if (!sessionResult.Success)
            {
                return sessionResult;
            }

            var session = sessionResult.Value;

            // Validate session and user id (defensive guards)
            var invalidSessionResult = ValidateLoginSession(session, email);
            if (invalidSessionResult != null)
            {
                return invalidSessionResult;
            }

            var userIdStr = session!.User!.Id;
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Result.Fail("Invalid user ID.");
            }
            var authUserId = Guid.Parse(userIdStr);

            // Set temporary session for profile retrieval
            _sessionService.SetUser(authUserId, session.User.Email ?? email, session.AccessToken);

            // Verify master password and derive key
            var verificationResult = await VerifyMasterPasswordAsync(authUserId, masterPassword);
            if (!verificationResult.Success)
            {
                _sessionService.ClearSession();
                await _supabase.Auth.SignOut();
                return verificationResult;
            }

            var key = verificationResult.Value;
            _sessionService.SetDerivedKey(key);

            _logger.LogInformation("User {UserId} logged in successfully", authUserId);
            return Result.Ok();
        }

        public async Task<Result> VerifyEmailConfirmationAsync(string email, string otpCode)
        {
            try
            {
                var session = await _supabase.Auth.VerifyOTP(email, otpCode, Constants.EmailOtpType.Signup);
                if (session?.User == null)
                {
                    return Result.Fail(AuthMessages.OtpInvalidOrExpired);
                }
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email confirmation failed for {Email}", email);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        public async Task<Result> VerifyPasswordResetAsync(string email, string otpCode)
        {
            try
            {
                var session = await _supabase.Auth.VerifyOTP(email, otpCode, Constants.EmailOtpType.Recovery);
                if (session?.User == null)
                    return Result.Fail(AuthMessages.OtpInvalidOrExpired);
                
                var userId = session.User.Id;
                if (string.IsNullOrEmpty(userId))
                    return Result.Fail("Invalid user ID.");
                
                var userIdGuid = Guid.Parse(userId);
                _sessionService.SetUser(userIdGuid, session.User.Email ?? email, session.AccessToken);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password reset failed for {Email}", email);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        public async Task<Result> SendResetPasswordEmailAsync(string email)
        {
            try
            {
                await _supabase.Auth.ResetPasswordForEmail(email);
                _logger.LogInformation("Password reset email sent to {Email}", email);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password reset email failed for {Email}", email);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        public async Task<Result> SendOTPConfirmationAsync(string email)
        {
            try
            {
                await _supabase.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email));
                _logger.LogInformation("Sent OTP confirmation email to {Email}", email);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email confirmation resend failed for {Email}", email);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        [ExcludeFromCodeCoverage]
        public async Task LockAsync()
        {
            try
            {
                await _supabase.Auth.SignOut();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during sign out");
            }
            finally
            {
                _sessionService.ClearSession();
                _logger.LogInformation("Session locked");
            }
        }

        public bool IsLocked()
        {
            // Check both Supabase session and internal session
            var hasSupabaseSession = _supabase.Auth.CurrentSession != null;
            var hasInternalSession = _sessionService.IsActive();

            return !hasSupabaseSession || !hasInternalSession;
        }

        public async Task<Result> ChangeMasterPasswordAsync(string currentPassword, string newPassword)
        {
            var userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result.Fail("Not logged in.");

            var validationResult = _passwordValidator.Validate(new PasswordInput { Password = newPassword });
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result.Fail(errors);
            }

            var profileResult = await _userProfileService.GetProfileAsync(userId.Value);
            if (!profileResult.Success)
                return Result.Fail("Failed to load user profile.");

            var profile = profileResult.Value;
            var salt = Convert.FromBase64String(profile.Salt);
            var currentKek = _cryptoService.DeriveKey(currentPassword, salt);

            byte[] dek;
            if (string.IsNullOrEmpty(profile.EncryptedDEK))
                return Result.Fail("Invalid email or password.");

            // DEK path: decrypt DEK with current KEK
            var encryptedDekResult = EncryptedBlob.FromBase64String(profile.EncryptedDEK);
            if (!encryptedDekResult.Success)
                return Result.Fail("Invalid email or password.");

            var dekResult = _cryptoService.Decrypt(encryptedDekResult.Value, currentKek);
            if (!dekResult.Success)
                return Result.Fail("Invalid email or password.");

            dek = Convert.FromBase64String(dekResult.Value);

            // Generate new salt and KEK for new password
            var newSalt = _cryptoService.GenerateSalt();
            var newKek = _cryptoService.DeriveKey(newPassword, newSalt);

            // Re-wrap DEK with new KEK
            var dekBase64 = Convert.ToBase64String(dek);
            var encryptedNewDekResult = _cryptoService.Encrypt(dekBase64, newKek);
            if (!encryptedNewDekResult.Success)
                return Result.Fail("Failed to re-encrypt vault key. Please try again.");
            
            try
            {
                await _supabase.Auth.Update(new UserAttributes { Password = newPassword });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Supabase Auth password for user {UserId}", userId);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }

            profile.Salt = Convert.ToBase64String(newSalt);
            profile.EncryptedDEK = encryptedNewDekResult.Value.ToBase64String();

            try
            {
                await _vaultRepository.UpdateUserProfileAsync(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user profile after password change for user {UserId}", userId);
                return Result.Fail("Failed to save profile changes. Please try again.");
            }

            _sessionService.SetDerivedKey(dek);
            _logger.LogInformation("Master password changed successfully for user {UserId}", userId);
            return Result.Ok();
        }

        public async Task<Result<string>> SetupRecoveryKeyAsync()
        {
            var userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result<string>.Fail("Not logged in.");

            var profileResult = await _userProfileService.GetProfileAsync(userId.Value);
            if (!profileResult.Success)
                return Result<string>.Fail("Failed to load user profile.");

            var profile = profileResult.Value;
            if (string.IsNullOrEmpty(profile.EncryptedDEK))
                return Result<string>.Fail("Recovery key setup is not supported for legacy accounts.");

            // Do not overwrite an existing recovery key. It is meant to be shown once to the user.
            if (!string.IsNullOrEmpty(profile.RecoveryEncryptedDEK) && !string.IsNullOrEmpty(profile.RecoverySalt))
                return Result<string>.Fail("Recovery key is already set up for this account.");

            byte[] dek;
            try
            {
                dek = _sessionService.GetDerivedKey();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve session key for user {UserId}", userId);
                return Result<string>.Fail("Session key not available. Please log in again.");
            }

            // random 32-byte recovery key
            var recoveryKeyBytes = new byte[32];
            RandomNumberGenerator.Fill(recoveryKeyBytes);
            var recoveryKeyBase64 = Convert.ToBase64String(recoveryKeyBytes);

            var recoverySalt = _cryptoService.GenerateSalt();
            var recoveryKek = _cryptoService.DeriveKey(recoveryKeyBase64, recoverySalt);

            var dekBase64 = Convert.ToBase64String(dek);
            var encryptedRecoveryDekResult = _cryptoService.Encrypt(dekBase64, recoveryKek);
            if (!encryptedRecoveryDekResult.Success)
                return Result<string>.Fail("Failed to create recovery key. Please try again.");

            profile.RecoveryEncryptedDEK = encryptedRecoveryDekResult.Value.ToBase64String();
            profile.RecoverySalt = Convert.ToBase64String(recoverySalt);

            try
            {
                await _vaultRepository.UpdateUserProfileAsync(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save recovery key for user {UserId}", userId);
                return Result<string>.Fail("Failed to save recovery key. Please try again.");
            }

            _logger.LogInformation("Recovery key set up for user {UserId}", userId);
            return Result<string>.Ok(recoveryKeyBase64);
        }

        public async Task<Result> RecoverVaultAsync(string recoveryKey, string newMasterPassword)
        {
            var userId = _sessionService.CurrentUserId;
            if (userId == null)
                return Result.Fail("Not logged in.");

            var validationResult = _passwordValidator.Validate(new PasswordInput { Password = newMasterPassword });
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result.Fail(errors);
            }

            var profileResult = await _userProfileService.GetProfileAsync(userId.Value);
            if (!profileResult.Success)
                return Result.Fail("Failed to load user profile.");

            var profile = profileResult.Value;

            if (string.IsNullOrEmpty(profile.RecoveryEncryptedDEK) || string.IsNullOrEmpty(profile.RecoverySalt))
                return Result.Fail("No recovery key has been set up for this account.");

            // Derive recovery KEK from provided recovery key
            var recoverySalt = Convert.FromBase64String(profile.RecoverySalt);
            var recoveryKek = _cryptoService.DeriveKey(recoveryKey, recoverySalt);

            // Decrypt DEK using recovery KEK
            var encryptedRecoveryDekResult = EncryptedBlob.FromBase64String(profile.RecoveryEncryptedDEK);
            if (!encryptedRecoveryDekResult.Success)
                return Result.Fail("Invalid recovery data.");

            var dekResult = _cryptoService.Decrypt(encryptedRecoveryDekResult.Value, recoveryKek);
            if (!dekResult.Success)
                return Result.Fail("Invalid recovery key.");

            var dek = Convert.FromBase64String(dekResult.Value);

            // Generate new salt and KEK for new password
            var newSalt = _cryptoService.GenerateSalt();
            var newKek = _cryptoService.DeriveKey(newMasterPassword, newSalt);

            var dekBase64 = Convert.ToBase64String(dek);
            var encryptedNewDekResult = _cryptoService.Encrypt(dekBase64, newKek);
            if (!encryptedNewDekResult.Success)
                return Result.Fail("Failed to re-encrypt vault key. Please try again.");

            // Update Supabase Auth password
            try
            {
                await _supabase.Auth.Update(new UserAttributes { Password = newMasterPassword });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Supabase Auth password during recovery for user {UserId}", userId);
                return Result.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }

            profile.Salt = Convert.ToBase64String(newSalt);
            profile.EncryptedDEK = encryptedNewDekResult.Value.ToBase64String();

            try
            {
                await _vaultRepository.UpdateUserProfileAsync(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user profile during recovery for user {UserId}", userId);
                return Result.Fail("Failed to save profile changes. Please try again.");
            }

            _sessionService.SetDerivedKey(dek);
            _logger.LogInformation("Vault recovered successfully for user {UserId}", userId);
            return Result.Ok();
        }

        private Result ValidateCredentials(string email, string masterPassword)
        {
            var emailValidationResult = _emailValidator.Validate(new EmailInput { Email = email });
            if (!emailValidationResult.IsValid)
            {
                var errors = string.Join("; ", emailValidationResult.Errors.Select(e => e.ErrorMessage));
                return Result.Fail(errors);
            }

            var passwordValidationResult = _passwordValidator.Validate(new PasswordInput { Password = masterPassword });
            if (!passwordValidationResult.IsValid)
            {
                var errors = string.Join("; ", passwordValidationResult.Errors.Select(e => e.ErrorMessage));
                return Result.Fail(errors);
            }

            return Result.Ok();
        }

        [ExcludeFromCodeCoverage]
        private Result? ValidateSignUpSession(Session? session, string email)
        {
            if (session?.User == null)
            {
                _logger.LogInformation("User registered but email confirmation required for {Email}", email);
                return Result.Fail("Registration successful! Please check your email to confirm your account before signing in.");
            }

            if (string.IsNullOrEmpty(session.User.Id))
            {
                _logger.LogError("User ID is null or empty after signup for {Email}", email);
                _supabase.Auth.SignOut();
                return Result.Fail("Registration failed. Invalid user ID.");
            }

            return null;
        }

        [ExcludeFromCodeCoverage]
        private Result? ValidateLoginSession(Session? session, string email)
        {
            if (session?.User == null)
            {
                return Result.Fail("Invalid session. Please try again.");
            }

            if (string.IsNullOrEmpty(session.User.Id))
            {
                _logger.LogError("User ID is null or empty after login for {Email}", email);
                return Result.Fail("Login failed. Invalid user ID.");
            }

            return null;
        }

        [ExcludeFromCodeCoverage]
        private async Task<Result<Session?>> CreateAuthSessionAsync(
            string email,
            string masterPassword,
            Dictionary<string, object>? signUpMetadata = null)
        {
            try
            {
                var options = signUpMetadata != null
                    ? new SignUpOptions { Data = signUpMetadata }
                    : null;
                var session = await _supabase.Auth.SignUp(email, masterPassword, options);

                return Result<Session?>.Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration failed for {Email}", email);
                return Result<Session?>.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        [ExcludeFromCodeCoverage]
        private async Task<Result<Session>> AuthenticateAsync(string email, string masterPassword)
        {
            try
            {
                var session = await _supabase.Auth.SignIn(email, masterPassword);

                if (session?.User == null)
                {
                    return Result<Session>.Fail("Invalid email or password.");
                }

                return Result<Session>.Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Login failed for {Email}", email);
                return Result<Session>.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

        private (byte[] salt, byte[] key) CreateCryptographicMaterials(string masterPassword)
        {
            var salt = _cryptoService.GenerateSalt();
            var key = _cryptoService.DeriveKey(masterPassword, salt);
            return (salt, key);
        }

        private async Task<Result<byte[]>> VerifyMasterPasswordAsync(Guid userId, string masterPassword)
        {
            var profileResult = await _userProfileService.GetProfileAsync(userId);
            if (!profileResult.Success)
            {
                return Result<byte[]>.Fail("User profile not found.");
            }

            var profile = profileResult.Value;
            var salt = Convert.FromBase64String(profile.Salt);
            var kek = _cryptoService.DeriveKey(masterPassword, salt);

            try
            {
                if (string.IsNullOrEmpty(profile.EncryptedDEK))
                    return Result<byte[]>.Fail("Invalid email or password.");

                // DEK path: decrypt the DEK with the password-derived KEK; store DEK as session key
                var encryptedDekResult = EncryptedBlob.FromBase64String(profile.EncryptedDEK);
                if (!encryptedDekResult.Success)
                    return Result<byte[]>.Fail("Invalid email or password.");

                var dekResult = _cryptoService.Decrypt(encryptedDekResult.Value, kek);
                if (!dekResult.Success)
                    return Result<byte[]>.Fail("Invalid email or password.");

                return Result<byte[]>.Ok(Convert.FromBase64String(dekResult.Value));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password verification failed for user {UserId}", userId);
                return Result<byte[]>.Fail("Invalid email or password.");
            }
        }
    }
}