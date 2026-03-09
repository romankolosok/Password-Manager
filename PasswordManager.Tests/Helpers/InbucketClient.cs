using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PasswordManager.Tests.Helpers
{
    public static class InbucketClient
    {
        private static readonly IConfiguration _config = BuildTestConfiguration();
        private static readonly HttpClient _http = new() { BaseAddress = new Uri(_config["Inbucket:Url"] ?? "http://127.0.0.1:54324") };
        private static readonly Regex OtpRegex = new(@"\b\d{8}\b", RegexOptions.Compiled); // 8-digit OTP

        // Mailpit /api/v1/messages response
        private sealed record MessagesResponse([property: JsonPropertyName("messages")] List<MessageSummary>? Messages);
        private sealed record MessageSummary([property: JsonPropertyName("ID")] string ID);

        // Mailpit /api/v1/message/{ID} response
        private sealed record MessageDetail(
            [property: JsonPropertyName("Text")] string? Text,
            [property: JsonPropertyName("HTML")] string? HTML
        );

        public static async Task<string?> GetLatestOtpAsync(string email, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var deadline = DateTime.UtcNow + timeout.Value;

            while (DateTime.UtcNow < deadline)
            {
                var query = Uri.EscapeDataString($"to:\"{email}\"");
                var response = await _http.GetFromJsonAsync<MessagesResponse>($"/api/v1/messages?query={query}");
                var message = response?.Messages?.FirstOrDefault();

                if (message != null)
                {
                    var detail = await _http.GetFromJsonAsync<MessageDetail>($"/api/v1/message/{message.ID}");
                    if (detail != null)
                    {
                        // Our signup.html is just {{ .Token }}, so the body is the OTP token itself.
                        var body = detail.Text?.Trim() ?? detail.HTML?.Trim() ?? string.Empty;
                        var match = OtpRegex.Match(body);
                        if (match.Success)
                            return match.Value;
                    }
                }

                await Task.Delay(1000);
            }

            return null; // no OTP found in time
        }

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
    }
}
