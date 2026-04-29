using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace PasswordManager.App.Services
{
    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private const string LatestReleaseUrl =
            "https://api.github.com/repos/romankolosok/Password-Manager/releases/latest";

        public GitHubUpdateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(LatestReleaseUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tagName = root.GetProperty("tag_name").GetString();
                var htmlUrl = root.GetProperty("html_url").GetString();

                if (string.IsNullOrEmpty(tagName))
                    return new UpdateCheckResult(false, null, null);

                var latestVersionStr = tagName.TrimStart('v');
                if (!Version.TryParse(latestVersionStr, out var latestVersion))
                    return new UpdateCheckResult(false, null, null);

                var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
                if (assemblyVersion is null)
                    return new UpdateCheckResult(false, null, null);

                var currentVersion = new Version(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
                var updateAvailable = latestVersion > currentVersion;

                return new UpdateCheckResult(updateAvailable, tagName, htmlUrl);
            }
            catch
            {
                return new UpdateCheckResult(false, null, null);
            }
        }
    }
}
