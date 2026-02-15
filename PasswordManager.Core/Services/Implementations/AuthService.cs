using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Core.Validators;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly ICryptoService _cryptoService;
        private readonly IVaultRepository _vaultRepository;
        private readonly ISessionService _sessionService;
        private readonly PasswordValidator _passwordValidator = new();
        private readonly EmailValidator _emailValidator = new();

        public Guid? CurrentUserId => _sessionService.CurrentUserId;
        public string? CurrentUserEmail => _sessionService.CurrentUserEmail;

        public AuthService(ICryptoService cryptoService,
            IVaultRepository vaultRepository,
            ISessionService sessionService)
        {
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

            var existingUser = await _vaultRepository.GetUserByEmailAsync(email);

            if (existingUser != null)
            {
                return Result.Fail("An account with this email already exists.");
            }

            var salt = _cryptoService.GenerateSalt();
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            var verificationToken = new byte[32];
            RandomNumberGenerator.Fill(verificationToken);
            var tokenString = Convert.ToBase64String(verificationToken);

            var encryptedTokenResult = _cryptoService.Encrypt(tokenString, key);
            if (!encryptedTokenResult.Success)
                return Result.Fail(encryptedTokenResult.Message ?? "Failed to create account. Please try again.");

            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = email,
                Salt = Convert.ToBase64String(salt),
                EncryptedVerificationToken = encryptedTokenResult.Value.ToBase64String(),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _vaultRepository.CreateUserAsync(user);
            }
            catch (Exception)
            {
                return Result.Fail("Failed to create account. Please try again.");
            }

            _sessionService.SetUser(user.Id, user.Email);
            _sessionService.SetDerivedKey(key);

            return Result.Ok();
        }


        public async Task<Result> LoginAsync(string email, string masterPassword)
        {
            var user = await _vaultRepository.GetUserByEmailAsync(email);

            if (user == null)
            {
                return Result.Fail("Invalid email or password.");
            }

            var salt = Convert.FromBase64String(user.Salt);
            var key = _cryptoService.DeriveKey(masterPassword, salt);

            try
            {
                var encryptionToken = EncryptedBlob.FromBase64String(user.EncryptedVerificationToken);

                if (!encryptionToken.Success)
                {
                    return Result.Fail("Invalid email or password.");
                }

                var decryptedToken = _cryptoService.Decrypt(encryptionToken.Value, key);

                if (decryptedToken.Success)
                {
                    _sessionService.SetUser(user.Id, user.Email);
                    _sessionService.SetDerivedKey(key);
                    return Result.Ok();
                }
                else
                {
                    return Result.Fail("Invalid email or password.");
                }
            }
            catch (Exception)
            {
                return Result.Fail("Invalid email or password.");
            }
        }

        public void Lock()
        {
            _sessionService.ClearSession();
        }

        public bool IsLocked()
        {
            return !_sessionService.IsActive();
        }

        public Task<Result> ChangeMasterPasswordAsync(string currentPassword, string newPassword)
        {
            // TODO: implement (re-encrypt verification token with new key, update DB).
            return Task.FromResult(Result.Fail("Not implemented."));
        }
    }
}
