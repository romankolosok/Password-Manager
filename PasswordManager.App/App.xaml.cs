using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;
using PasswordManager.App.Views;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using System.IO;
using System.Windows;

namespace PasswordManager.App
{
    public partial class App : Application
    {
        public IServiceProvider? ServiceProvider { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // STEP 1: Build configuration (supports environment-specific overrides and build config)
            var environment =
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // If no explicit environment is set, fall back to build configuration:
            // - Debug build => Development (local Supabase)
            // - Release build => Production (cloud Supabase)
#if DEBUG
            environment ??= "Development";
#else
            environment ??= "Production";
#endif

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>()
                .Build();

            // Prefer environment variables (for CI) but fall back to configuration files.
            string? supabaseUrl =
                Environment.GetEnvironmentVariable("Supabase__Url") ??
                configuration["Supabase:Url"];

            string? supabaseAnonKey =
                Environment.GetEnvironmentVariable("Supabase__AnonKey") ??
                configuration["Supabase:AnonKey"];

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseAnonKey))
            {
                throw new InvalidOperationException(
                    "Supabase configuration is missing. Ensure Supabase:Url and Supabase:AnonKey are set " +
                    "in appsettings.json / appsettings.{Environment}.json or via Supabase__Url and Supabase__AnonKey environment variables.");
            }

            // Create service collection and register everything
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);

            // Supabase client
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false // Only enable if using realtime features
            };
            var supabase = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
            await supabase.InitializeAsync();

            services.AddSingleton(supabase);

            // Core services
            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IVaultRepository, VaultRepository>();
            services.AddSingleton<IVaultService, VaultService>();
            services.AddSingleton<IPasswordStrengthChecker, ZxcvbnPasswordStrengthChecker>();
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();

            services.AddSingleton<IClipboardService, WpfClipboardService>();
            services.AddSingleton<IAuthCoordinator, AuthCoordinator>();
            services.AddSingleton<IUserProfileService, UserProfileService>();
            services.AddSingleton<ISupabaseExceptionMapper, SupabaseExceptionMapper>();
            services.AddLogging();

            // ViewModels and Views
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<VaultListViewModel>();
            services.AddTransient<EntryDetailViewModel>();
            services.AddTransient<LoginView>();
            services.AddTransient<RegisterView>();
            services.AddTransient<VaultListView>();
            services.AddTransient<EntryDetailView>();
            services.AddSingleton<MainWindow>();

            //Build the service provider
            ServiceProvider = services.BuildServiceProvider();

            //Show the login window via coordinator
            var coordinator = ServiceProvider.GetRequiredService<IAuthCoordinator>();
            coordinator.ShowLogin();
        }
    }
}