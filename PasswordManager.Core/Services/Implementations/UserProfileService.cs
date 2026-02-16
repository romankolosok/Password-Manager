using Microsoft.Extensions.Configuration;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IVaultRepository _vaultRepository;
        private static readonly JsonSerializerOptions JsonOptions = new();

        public UserProfileService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IVaultRepository vaultRepository)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _vaultRepository = vaultRepository;
        }

        public async Task<Result> CreateProfileAsync(UserProfileEntity profile, string accessToken)
        {
            var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
            var anonKey = _configuration["Supabase:AnonKey"];

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(anonKey))
            {
                return Result.Fail("Supabase configuration is missing.");
            }

            try
            {
                await InsertUserProfileAsync(supabaseUrl, anonKey, accessToken, profile);
                return Result.Ok();
            }
            catch (HttpRequestException ex)
            {
                return Result.Fail($"Network error while creating profile: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result.Fail($"Failed to create user profile: {ex.Message}");
            }
        }

        public async Task<Result<UserProfileEntity>> GetProfileAsync(Guid userId)
        {
            var profile = await _vaultRepository.GetUserProfileAsync(userId);

            if (profile == null)
            {
                return Result<UserProfileEntity>.Fail("User profile not found.");
            }

            return Result<UserProfileEntity>.Ok(profile);
        }

        private async Task InsertUserProfileAsync(
            string supabaseUrl,
            string anonKey,
            string accessToken,
            UserProfileEntity profile)
        {
            var payload = new
            {
                profile.Id,
                profile.Salt,
                profile.EncryptedVerificationToken,
                profile.CreatedAt
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/UserProfiles");
            request.Headers.Add("apikey", anonKey);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }
    }
}