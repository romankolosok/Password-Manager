using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PasswordManager.Core.Data;

namespace PasswordManager.App.Data
{
    /// <summary>
    /// Used only by the dotnet ef tool at design time to create <see cref="VaultDbContext"/>.
    /// At runtime the app uses DI (e.g. AddVaultDbContext in App.xaml.cs).
    /// </summary>
    internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VaultDbContext>
    {
        public VaultDbContext CreateDbContext(string[] args)
        {
            // Build configuration: appsettings.json (optional) + user secrets (recommended for connection string).
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<DesignTimeDbContextFactory>()
                .Build();

            string? connectionString = configuration["Database:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Set the database connection string for design-time tools. " +
                    "Options: user secret 'Database:ConnectionString' (dotnet user-secrets set \"Database:ConnectionString\" \"Host=...;Database=...;Username=...;Password=...\") " +
                    "or appsettings.json with a 'Database:ConnectionString' key.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<VaultDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new VaultDbContext(optionsBuilder.Options);
        }
    }
}
