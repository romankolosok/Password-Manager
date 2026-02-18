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