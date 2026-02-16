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
    /// <summary>
    /// Composition root — wires all interfaces to their implementations.
    /// Uses Supabase Auth + REST API (no direct database connection).
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider? ServiceProvider { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // STEP 1: Build configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<App>()
                .Build();

            string? supabaseUrl = configuration["Supabase:Url"];
            string? supabaseAnonKey = configuration["Supabase:AnonKey"];
            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseAnonKey))
                throw new InvalidOperationException("Supabase:Url and Supabase:AnonKey must be set in appsettings.json.");

            // STEP 2: Create service collection and register everything
            var services = new ServiceCollection();
            services.AddHttpClient();

            services.AddSingleton<IConfiguration>(configuration);

            // Supabase client (replaces direct DB connection; Auth + REST use auth.uid() RLS)
            var supabase = new Supabase.Client(supabaseUrl, supabaseAnonKey);
            await supabase.InitializeAsync();
            services.AddSingleton(supabase);

            // Core services
            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IVaultRepository, VaultRepository>();
            services.AddSingleton<IVaultService, VaultService>();

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

            // STEP 3: Build the service provider
            ServiceProvider = services.BuildServiceProvider();

            // STEP 4: Show the login window via coordinator
            var coordinator = ServiceProvider.GetRequiredService<IAuthCoordinator>();
            coordinator.ShowLogin();
        }
    }
}
