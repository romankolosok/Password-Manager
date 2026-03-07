using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;
using PasswordManager.App.Views;
using PasswordManager.Core.Exceptions;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.App
{
    public partial class App : Application
    {
        public IServiceProvider? ServiceProvider { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            var environment =
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

#if DEBUG
            environment ??= "Development";
#else
            environment ??= "Production";
#endif

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

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

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);

            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };
            var supabase = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
            await supabase.InitializeAsync();

            services.AddSingleton(supabase);

            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IVaultRepository, VaultRepository>();
            services.AddSingleton<IVaultService, VaultService>();
            services.AddSingleton<IPasswordStrengthChecker, ZxcvbnPasswordStrengthChecker>();
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();

            services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
            services.AddSingleton<IAuthCoordinator, AuthCoordinator>();
            services.AddSingleton<IUserProfileService, UserProfileService>();
            services.AddSingleton<ISupabaseExceptionMapper, SupabaseExceptionMapper>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddLogging();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<VaultListViewModel>();
            services.AddTransient<EntryDetailViewModel>();
            services.AddTransient<LoginView>();
            services.AddTransient<RegisterView>();
            services.AddTransient<VaultListView>();
            services.AddTransient<EntryDetailView>();
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;
            }

            var coordinator = ServiceProvider.GetRequiredService<IAuthCoordinator>();
            coordinator.ShowLogin();

            base.OnFrameworkInitializationCompleted();
        }
    }
}
