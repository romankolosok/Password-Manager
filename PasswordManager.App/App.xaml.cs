using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;
using PasswordManager.App.Views;
using PasswordManager.Core.Data;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using System.IO;
using System.Windows;

namespace PasswordManager.App
{
    /// <summary>
    /// Composition root — wires all interfaces to their implementations.
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // STEP 1: Build configuration
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<App>()
                .Build();

            // STEP 2: Create service collection and register everything
            var services = new ServiceCollection();

            // Configuration
            services.AddSingleton<IConfiguration>(configuration);

            // Database
            string? connectionString = configuration["Database:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Database:ConnectionString is not set. Use user secrets or appsettings.json.");
            services.AddVaultDbContext(connectionString);

            // Core services (Singleton = one instance for the entire app lifetime)
            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<ISessionService, SessionService>();

            // Scoped services (one instance per scope; DbContext is scoped)
            services.AddScoped<IVaultRepository, VaultRepository>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IVaultService, VaultService>();

            // WPF-specific services
            services.AddSingleton<IClipboardService, WpfClipboardService>();

            // Auth flow coordination
            services.AddSingleton<IAuthCoordinator, AuthCoordinator>();

            // ViewModels (Transient = new instance each time requested)
            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<VaultListViewModel>();
            services.AddTransient<EntryDetailViewModel>();

            // Windows
            services.AddTransient<LoginView>();
            services.AddTransient<RegisterView>();
            services.AddTransient<VaultListView>();
            services.AddSingleton<MainWindow>();

            // STEP 3: Build the service provider
            ServiceProvider = services.BuildServiceProvider();

            // STEP 4: Show the login window via coordinator (handles login/register/main flow)
            var coordinator = ServiceProvider.GetRequiredService<IAuthCoordinator>();
            coordinator.ShowLogin();
        }
    }
}