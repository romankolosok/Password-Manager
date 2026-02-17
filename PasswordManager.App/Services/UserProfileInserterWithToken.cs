using Microsoft.Extensions.Configuration;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordManager.App.Services
{
    /// <summary>
    /// Inserts UserProfile via direct POST with JWT so RLS sees the authenticated user.
    /// Used only for registration when the Supabase client does not attach the new session to the next request.
    /// </summary>
    internal sealed class UserProfileInserterWithToken : IUserProfileInserterWithToken
    {
        private static readonly HttpClient SharedClient = new();
        private readonly string _restUrl;
        private readonly string _anonKey;

        // PostgREST expects column names as in DB: Id, Salt, EncryptedVerificationToken, CreatedAt
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null
        };

        public UserProfileInserterWithToken(IConfiguration configuration)
        {
            var url = configuration["Supabase:Url"]?.TrimEnd('/') ?? throw new InvalidOperationException("Supabase:Url is not set.");
            _restUrl = $"{url}/rest/v1";
            _anonKey = configuration["Supabase:AnonKey"] ?? throw new InvalidOperationException("Supabase:AnonKey is not set.");
        }

        public async Task<Result> InsertAsync(UserProfileEntity profile, string accessToken)
        {
            var payload = new
            {
                Id = profile.Id,
                Salt = profile.Salt,
                EncryptedVerificationToken = profile.EncryptedVerificationToken,
                CreatedAt = profile.CreatedAt
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_restUrl}/UserProfiles");
            request.Headers.Add("apikey", _anonKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await SharedClient.SendAsync(request).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
                return Result.Ok();

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return Result.Fail($"Database error while creating profile: {response.StatusCode} {body}");
        }
    }
}
