using Microsoft.Extensions.Logging;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using Supabase.Gotrue;
using System;
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
        private readonly ISessionService _sessionService;
        private readonly ISupabaseExceptionMapper _exceptionMapper;
        private readonly ILogger<AuthService> _logger;
        private readonly PasswordValidator _passwordValidator = new();
        private readonly EmailValidator _emailValidator = new();

        public Guid? CurrentUserId => _sessionService.CurrentUserId;
        public string? CurrentUserEmail => _sessionService.CurrentUserEmail;

        private readonly IUserProfileInserterWithToken? _profileInserterWithToken;

        public AuthService(
            Supabase.Client supabase,
            ICryptoService cryptoService,
            IUserProfileService userProfileService,
            ISessionService sessionService,
            ISupabaseExceptionMapper exceptionMapper,
            ILogger<AuthService> logger,
            IUserProfileInserterWithToken? profileInserterWithToken = null)
        {
            _supabase = supabase;
            _cryptoService = cryptoService;
            _userProfileService = userProfileService;
            _sessionService = sessionService;
            _exceptionMapper = exceptionMapper;
            _logger = logger;
            _profileInserterWithToken = profileInserterWithToken;

            _supabase.Auth.AddStateChangedListener(OnAuthStateChanged);
        }

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

            // Attempt registration
            var sessionResult = await CreateAuthSessionAsync(email, masterPassword);
            if (!sessionResult.Success)
            {
                return sessionResult;
            }

            var session = sessionResult.Value;

            // Handle case where email confirmation is required
            if (session == null || session.User == null)
            {
                _logger.LogInformation("User registered but email confirmation required for {Email}", email);
                return Result.Fail("Registration successful! Please check your email to confirm your account before signing in.");
            }

            // Parse user ID with null check
            if (string.IsNullOrEmpty(session.User.Id))
            {
                _logger.LogError("User ID is null or empty after signup for {Email}", email);
                return Result.Fail("Registration failed. Invalid user ID.");
            }

            var authUserId = Guid.Parse(session.User.Id);

            var (salt, key, verificationToken) = CreateCryptographicMaterials(masterPassword);

            var profileBuildResult = BuildUserProfileEntity(authUserId, salt, verificationToken, key);
            if (!profileBuildResult.Success)
            {
                await _supabase.Auth.SignOut();
                return profileBuildResult;
            }

            var profile = profileBuildResult.Value;
            var profileResult = _profileInserterWithToken != null
                ? await _profileInserterWithToken.InsertAsync(profile, session.AccessToken!)
                : await _userProfileService.CreateProfileAsync(profile);

            if (!profileResult.Success)
            {
                _logger.LogError("Failed to create profile for user {UserId}", authUserId);
                await _supabase.Auth.SignOut();
                return profileResult;
            }

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

            // Validate session
            if (session?.User == null)
            {
                return Result.Fail("Invalid session. Please try again.");
            }

            // Parse user ID with null check
            if (string.IsNullOrEmpty(session.User.Id))
            {
                _logger.LogError("User ID is null or empty after login for {Email}", email);
                return Result.Fail("Login failed. Invalid user ID.");
            }

            var authUserId = Guid.Parse(session.User.Id);

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

        public Task<Result> ChangeMasterPasswordAsync(string currentPassword, string newPassword)
        {
            _logger.LogWarning("ChangeMasterPassword called but not implemented");
            return Task.FromResult(Result.Fail("Not implemented."));
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

        private async Task<Result<Session?>> CreateAuthSessionAsync(string email, string masterPassword)
        {
            try
            {
                var session = await _supabase.Auth.SignUp(email, masterPassword);

                // Session can be null if email confirmation is required
                return Result<Session?>.Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration failed for {Email}", email);
                return Result<Session?>.Fail(_exceptionMapper.MapAuthException(ex).Message);
            }
        }

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
                return Result<Session>.Fail("Invalid email or password.");
            }
        }

        private (byte[] salt, byte[] key, string verificationToken) CreateCryptographicMaterials(string masterPassword)
        {
            var salt = _cryptoService.GenerateSalt();
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            var verificationTokenBytes = new byte[32];
            RandomNumberGenerator.Fill(verificationTokenBytes);
            var verificationToken = Convert.ToBase64String(verificationTokenBytes);

            return (salt, key, verificationToken);
        }

        private Result<UserProfileEntity> BuildUserProfileEntity(
            Guid userId,
            byte[] salt,
            string verificationToken,
            byte[] key)
        {
            var encryptedTokenResult = _cryptoService.Encrypt(verificationToken, key);
            if (!encryptedTokenResult.Success)
                return Result<UserProfileEntity>.Fail("Failed to create account. Please try again.");

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedVerificationToken = encryptedTokenResult.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };
            return Result<UserProfileEntity>.Ok(profile);
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
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            try
            {
                var encryptionToken = EncryptedBlob.FromBase64String(profile.EncryptedVerificationToken);
                if (!encryptionToken.Success)
                {
                    return Result<byte[]>.Fail("Invalid email or password.");
                }

                var decryptedToken = _cryptoService.Decrypt(encryptionToken.Value, key);
                if (!decryptedToken.Success)
                {
                    return Result<byte[]>.Fail("Invalid email or password.");
                }

                return Result<byte[]>.Ok(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password verification failed for user {UserId}", userId);
                return Result<byte[]>.Fail("Invalid email or password.");
            }
        }
    }
}