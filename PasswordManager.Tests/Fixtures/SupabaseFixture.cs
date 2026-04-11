using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Helpers;
using Supabase.Gotrue;

namespace PasswordManager.Tests.Fixtures
{
    /// <summary>
    /// Integration-test fixture that stands up a real Supabase.Client and the
    /// full dependency chain against the local Supabase Docker instance.
    /// Implements IAsyncLifetime because Supabase.Client.InitializeAsync() is async.
    /// Shared across AuthService and VaultRepository integration tests via IClassFixture.
    /// </summary>
    public class SupabaseFixture : IAsyncLifetime
    {
        /// <summary>
        /// All generated test emails use this domain so cleanup can target them precisely.
        /// </summary>
        private const string TestEmailDomain = "integration.local";

        private readonly List<string> _createdEmails = [];
        private int _emailCounter;

        public Supabase.Client SupabaseClient { get; private set; } = null!;
        /// <summary>
        /// Supabase client initialised with the service role key, if available.
        /// Used by integration tests that need to bypass RLS (e.g. deleting profile rows).
        /// </summary>
        public Supabase.Client? AdminSupabaseClient { get; private set; }
        public CryptoService CryptoService { get; } = new();
        public SessionService SessionService { get; private set; } = null!;
        public IAuthClient AuthClient { get; private set; } = null!;
        public VaultRepository VaultRepository { get; private set; } = null!;
        public UserProfileService UserProfileService { get; private set; } = null!;
        public SupabaseExceptionMapper ExceptionMapper { get; } = new();
        public ILogger<AuthService> Logger { get; } = new LoggerFactory().CreateLogger<AuthService>();
        public AuthService AuthService { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var config = BuildTestConfiguration();
            const string configHint =
                "For local integration tests: run 'supabase start', then either (1) set env vars Supabase__Url, Supabase__AnonKey, Supabase__ServiceRoleKey from 'supabase status' output, or (2) add PasswordManager.Tests/appsettings.Development.json (gitignored) with Supabase:Url, Supabase:AnonKey, Supabase:ServiceRoleKey. See appsettings.Example.json in the test project.";

            var supabaseUrl = GetSupabaseSetting(config, "Url")
                ?? throw new InvalidOperationException($"Supabase URL is not set. {configHint}");
            var supabaseAnonKey = GetSupabaseSetting(config, "AnonKey")
                ?? throw new InvalidOperationException($"Supabase AnonKey is not set. {configHint}");
            var serviceRoleKey = GetSupabaseSetting(config, "ServiceRoleKey");

            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };

            SupabaseClient = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
            await SupabaseClient.InitializeAsync();

            // Optional admin client for RLS-bypassing operations in tests (e.g. clean-up, data corruption scenarios).
            if (!string.IsNullOrWhiteSpace(serviceRoleKey))
            {
                var adminOptions = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = false,
                    AutoConnectRealtime = false
                };

                AdminSupabaseClient = new Supabase.Client(supabaseUrl, serviceRoleKey, adminOptions);
                await AdminSupabaseClient.InitializeAsync();
            }

            SessionService = new SessionService();
            AuthClient = new SupabaseAuthClient(SupabaseClient);
            VaultRepository = new VaultRepository(SupabaseClient);
            UserProfileService = new UserProfileService(VaultRepository);

            AuthService = new AuthService(
                AuthClient,
                CryptoService,
                UserProfileService,
                VaultRepository,
                SessionService,
                ExceptionMapper,
                Logger);
        }

        /// <summary>
        /// Generates a unique email address per test to avoid conflicts across parallel runs.
        /// All emails match the pattern <c>test_*@integration.local</c>.
        /// </summary>
        public string GenerateUniqueEmail()
        {
            var counter = Interlocked.Increment(ref _emailCounter);
            var email = $"test_{DateTime.UtcNow:yyyyMMddHHmmss}_{counter}@{TestEmailDomain}";
            _createdEmails.Add(email);
            return email;
        }

        /// <summary>
        /// Deletes every tracked <c>test_*@integration.local</c> user from the local
        /// Supabase Auth instance using the GoTrue <see cref="AdminClient"/> API
        /// (<see cref="AdminClient.ListUsers"/> and <see cref="AdminClient.DeleteUser"/>).
        /// Requires <c>Supabase__ServiceRoleKey</c> and <c>Supabase__Url</c>; silently skips if absent.
        /// Safe to call at any point; failures are swallowed so tests are not broken by cleanup issues.
        /// </summary>
        public async Task CleanupTestUsersAsync()
        {
            var config = BuildTestConfiguration();



            var serviceRoleKey = GetSupabaseSetting(config, "ServiceRoleKey");
            if (string.IsNullOrEmpty(serviceRoleKey))
                return;

            var supabaseUrl = GetSupabaseSetting(config, "Url");
            if (string.IsNullOrEmpty(supabaseUrl))
                return;

            var authUrl = supabaseUrl.TrimEnd('/') + "/auth/v1";
            var options = new ClientOptions { Url = authUrl };
            var adminClient = new AdminClient(serviceRoleKey, options);

            foreach (var email in _createdEmails)
            {
                try
                {
                    var list = await adminClient.ListUsers(filter: email);
                    if (list?.Users == null)
                        continue;

                    foreach (var user in list.Users)
                    {
                        if (user?.Id != null)
                            await adminClient.DeleteUser(user.Id);
                    }
                }
                catch
                {
                    // Best-effort: if admin cleanup fails, local Supabase resets will handle it
                }
            }

            _createdEmails.Clear();
        }

        public async Task<Result> ConfirmEmailAsync(string email)
        {
            var otp = await InbucketClient.GetLatestOtpAsync(email);

            if (otp == null)
            {
                return Result.Fail($"Failed to retrieve OTP for {email}.");
            }

            var otpVerifyResult = AuthService.VerifyEmailConfirmationAsync(email, otp);

            return await otpVerifyResult;
        }

        public async Task<Result<(Guid UserId, string Email)>> RegisterConfirmAndLoginAsync(string password)
        {
            var email = GenerateUniqueEmail();

            var result = await AuthService.RegisterAsync(email, password);
            if (!result.Success)
                return Result<(Guid UserId, string Email)>.Fail($"RegisterAsync failed: {result.Message}");

            var otpResult = await ConfirmEmailAsync(email);
            if (!otpResult.Success)
                return Result<(Guid UserId, string Email)>.Fail($"VerifyEmailConfirmationAsync failed: {otpResult.Message}");

            var loginResult = await AuthService.LoginAsync(email, password);
            if (!loginResult.Success)
                return Result<(Guid UserId, string Email)>.Fail($"LoginAsync failed: {loginResult.Message}");

            var userId = SessionService.CurrentUserId;
            if (userId is null)
            {
                return Result<(Guid UserId, string Email)>.Fail("LoginAsync succeeded but CurrentUserId is null.");
            }

            return Result<(Guid UserId, string Email)>.Ok((userId.Value, email));
        }

        public async Task DisposeAsync()
        {
            // Sign out any active session
            try
            {
                await SupabaseClient.Auth.SignOut();
            }
            catch
            {
                // Best-effort cleanup
            }

            await CleanupTestUsersAsync();

            SessionService?.Dispose();
        }

        /// <summary>
        /// Builds configuration from env vars and appsettings in the test output directory,
        /// so tests can run without setting env vars when appsettings.json is present.
        /// </summary>
        private static IConfiguration BuildTestConfiguration()
        {
            var basePath = AppContext.BaseDirectory;
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
        }

        /// <summary>
        /// Reads a Supabase setting.
        /// Order:
        /// - Env var "Supabase__{Key}"
        /// - Env vars emitted by `supabase status --output env` (API_URL, ANON_KEY, SERVICE_ROLE_KEY, etc.)
        /// - Config file key "Supabase:{Key}"
        /// </summary>
        private static string? GetSupabaseSetting(IConfiguration config, string key)
        {
            var envKey = $"Supabase__{key}";
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue;

            // 2) Supabase CLI env output conventions
            switch (key)
            {
                case "Url":
                    // `supabase status --output env`
                    envValue = Environment.GetEnvironmentVariable("API_URL");
                    if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
                    break;

                case "AnonKey":
                    envValue = Environment.GetEnvironmentVariable("ANON_KEY");
                    if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
                    envValue = Environment.GetEnvironmentVariable("PUBLISHABLE_KEY");
                    if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
                    break;

                case "ServiceRoleKey":
                    envValue = Environment.GetEnvironmentVariable("SERVICE_ROLE_KEY");
                    if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
                    envValue = Environment.GetEnvironmentVariable("SECRET_KEY");
                    if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
                    break;
            }

            return config[$"Supabase:{key}"];
        }
    }
}