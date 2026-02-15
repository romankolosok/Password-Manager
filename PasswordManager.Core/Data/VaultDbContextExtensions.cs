using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PasswordManager.Core.Data
{
    /// <summary>
    /// Registers <see cref="VaultDbContext"/> for Supabase (PostgreSQL). Use this in your app's DI setup
    /// instead of referencing Npgsql from the app project. Replaces the Cosmos-style registration:
    /// was options.UseCosmos(connectionString, databaseName).
    /// </summary>
    public static class VaultDbContextExtensions
    {
        /// <summary>
        /// Adds <see cref="VaultDbContext"/> with Npgsql for PostgreSQL/Supabase.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">Supabase/PostgreSQL connection string (e.g. from configuration or user secrets).</param>
        /// <param name="lifetime">Service lifetime; default is Scoped.</param>
        public static IServiceCollection AddVaultDbContext(
            this IServiceCollection services,
            string connectionString,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            return services.AddDbContext<VaultDbContext>(options =>
            {
                // PostgreSQL: UseNpgsql instead of UseCosmos(connectionString, databaseName).
                options.UseNpgsql(connectionString);
            }, lifetime);
        }
    }
}
