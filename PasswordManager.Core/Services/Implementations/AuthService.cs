using Microsoft.Extensions.Configuration;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using Supabase;
using Supabase.Gotrue;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly Supabase.Client _supabase;
        private readonly IConfiguration _configuration;
        private readonly ICryptoService _cryptoService;
        private readonly IVaultRepository _vaultRepository;
        private readonly ISessionService _sessionService;
        private readonly PasswordValidator _passwordValidator = new();
        private readonly EmailValidator _emailValidator = new();
        private static readonly JsonSerializerOptions JsonOptions = new();

        public Guid? CurrentUserId => _sessionService.CurrentUserId;
        public string? CurrentUserEmail => _sessionService.CurrentUserEmail;

        public AuthService(Supabase.Client supabase,
            IConfiguration configuration,
            ICryptoService cryptoService,
            IVaultRepository vaultRepository,
            ISessionService sessionService)
        {
            _supabase = supabase;
            _configuration = configuration;
            _cryptoService = cryptoService;
            _vaultRepository = vaultRepository;
            _sessionService = sessionService;
        }

        public async Task<Result> RegisterAsync(string email, string masterPassword)
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

            Session? session;
            try
            {
                session = await _supabase.Auth.SignUp(email, masterPassword);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("already registered", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("user_already_exists", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("422", StringComparison.OrdinalIgnoreCase))
                    return Result.Fail("An account with this email already exists. Sign in instead.");
                return Result.Fail(msg);
            }

            if (session?.User == null)
                return Result.Fail("Email confirmation may be required. Check your email, then sign in.");

            var authUserId = session.User.Id;
            var salt = _cryptoService.GenerateSalt();
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            var verificationToken = new byte[32];
            RandomNumberGenerator.Fill(verificationToken);
            var tokenString = Convert.ToBase64String(verificationToken);

            var encryptedTokenResult = _cryptoService.Encrypt(tokenString, key);
            if (!encryptedTokenResult.Success)
                return Result.Fail(encryptedTokenResult.Message ?? "Failed to create account. Please try again.");

            var profile = new UserProfileEntity
            {
                Id = Guid.Parse(authUserId),
                Salt = Convert.ToBase64String(salt),
                EncryptedVerificationToken = encryptedTokenResult.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            string? supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
            string? anonKey = _configuration["Supabase:AnonKey"];
            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(anonKey))
                return Result.Fail("Supabase configuration is missing.");

            try
            {
                await InsertUserProfileWithTokenAsync(supabaseUrl, anonKey, session.AccessToken ?? "", profile);
            }
            catch (Exception)
            {
                return Result.Fail("Could not create user profile.");
            }

            _sessionService.SetUser(Guid.Parse(authUserId), session.User.Email ?? email, session.AccessToken);
            _sessionService.SetDerivedKey(key);

            return Result.Ok();
        }

        public async Task<Result> LoginAsync(string email, string masterPassword)
        {
            Session? session;
            try
            {
                session = await _supabase.Auth.SignIn(email, masterPassword);
            }
            catch (Exception)
            {
                return Result.Fail("Invalid email or password.");
            }

            if (session?.User == null)
                return Result.Fail("Invalid email or password.");

            var authUserId = Guid.Parse(session.User.Id);

            _sessionService.SetUser(authUserId, session.User.Email ?? email, session.AccessToken);

            var profile = await _vaultRepository.GetUserProfileAsync(authUserId);
            if (profile == null)
            {
                _sessionService.ClearSession();
                return Result.Fail("User profile not found.");
            }

            var salt = Convert.FromBase64String(profile.Salt);
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            try
            {
                var encryptionToken = EncryptedBlob.FromBase64String(profile.EncryptedVerificationToken);
                if (!encryptionToken.Success)
                {
                    _sessionService.ClearSession();
                    return Result.Fail("Invalid email or password.");
                }

                var decryptedToken = _cryptoService.Decrypt(encryptionToken.Value, key);
                if (!decryptedToken.Success)
                {
                    _sessionService.ClearSession();
                    return Result.Fail("Invalid email or password.");
                }
            }
            catch (Exception)
            {
                _sessionService.ClearSession();
                return Result.Fail("Invalid email or password.");
            }

            _sessionService.SetDerivedKey(key);

            return Result.Ok();
        }

        public void Lock()
        {
            _ = _supabase.Auth.SignOut();
            _sessionService.ClearSession();
        }

        public bool IsLocked()
        {
            return !_sessionService.IsActive();
        }

        public Task<Result> ChangeMasterPasswordAsync(string currentPassword, string newPassword)
        {
            return Task.FromResult(Result.Fail("Not implemented."));
        }

        private static async Task InsertUserProfileWithTokenAsync(string supabaseUrl, string anonKey, string accessToken, UserProfileEntity profile)
        {
            var payload = new
            {
                profile.Id,
                profile.Salt,
                profile.EncryptedVerificationToken,
                profile.CreatedAt
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/UserProfiles");
            request.Headers.Add("apikey", anonKey);
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), System.Text.Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"UserProfiles insert failed: {response.StatusCode} {body}");
            }
        }
    }
}
