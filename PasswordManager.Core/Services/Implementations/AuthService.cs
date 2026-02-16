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

        public AuthService(
            Supabase.Client supabase,
            ICryptoService cryptoService,
            IUserProfileService userProfileService,
            ISessionService sessionService,
            ISupabaseExceptionMapper exceptionMapper,
            ILogger<AuthService> logger)
        {
            _supabase = supabase;
            _cryptoService = cryptoService;
            _userProfileService = userProfileService;
            _sessionService = sessionService;
            _exceptionMapper = exceptionMapper;
            _logger = logger;
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
            var authUserId = Guid.Parse(session.User!.Id);

            // Create cryptographic materials
            var (salt, key, verificationToken) = CreateCryptographicMaterials(masterPassword);

            // Create user profile
            var profileResult = await CreateUserProfileAsync(authUserId, salt, verificationToken, key, session.AccessToken!);
            if (!profileResult.Success)
            {
                _logger.LogError("Failed to create profile for user {UserId}", authUserId);
                return profileResult;
            }

            // Set session
            _sessionService.SetUser(authUserId, session.User.Email ?? email, session.AccessToken);
            _sessionService.SetDerivedKey(key);

            _logger.LogInformation("User {UserId} registered successfully", authUserId);
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
            var authUserId = Guid.Parse(session.User!.Id);

            // Set temporary session for profile retrieval
            _sessionService.SetUser(authUserId, session.User.Email ?? email, session.AccessToken);

            // Retriev      
            var verificationResult = await VerifyMasterPasswordAsync(authUserId, masterPassword);
            if (!verificationResult.Success)
            {
                _sessionService.ClearSession();
                return verificationResult;
            }

            var key = verificationResult.Value;
            _sessionService.SetDerivedKey(key);

            _logger.LogInformation("User {UserId} logged in successfully", authUserId);
            return Result.Ok();
        }

        public void Lock()
        {
            _ = _supabase.Auth.SignOut();
            _sessionService.ClearSession();
            _logger.LogInformation("Session locked");
        }

        public bool IsLocked()
        {
            return !_sessionService.IsActive();
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

        private async Task<Result<Session>> CreateAuthSessionAsync(string email, string masterPassword)
        {
            try
            {
                var session = await _supabase.Auth.SignUp(email, masterPassword);

                if (session?.User == null)
                {
                    return Result<Session>.Fail("Email confirmation may be required. Check your email, then sign in.");
                }

                return Result<Session>.Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration failed for {Email}", email);
                return Result<Session>.Fail(_exceptionMapper.MapAuthException(ex).Message);
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

        private async Task<Result> CreateUserProfileAsync(
            Guid userId,
            byte[] salt,
            string verificationToken,
            byte[] key,
            string accessToken)
        {
            var encryptedTokenResult = _cryptoService.Encrypt(verificationToken, key);
            if (!encryptedTokenResult.Success)
            {
                return Result.Fail("Failed to create account. Please try again.");
            }

            var profile = new UserProfileEntity
            {
                Id = userId,
                Salt = Convert.ToBase64String(salt),
                EncryptedVerificationToken = encryptedTokenResult.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            return await _userProfileService.CreateProfileAsync(profile, accessToken);
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