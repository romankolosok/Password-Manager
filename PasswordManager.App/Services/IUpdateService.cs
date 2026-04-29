namespace PasswordManager.App.Services
{
    public record UpdateCheckResult(bool UpdateAvailable, string? LatestVersion, string? ReleaseUrl);

    public interface IUpdateService
    {
        Task<UpdateCheckResult> CheckForUpdatesAsync();
    }
}
