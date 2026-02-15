using Microsoft.Extensions.Configuration;
using PasswordManager.Core.Entities;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class VaultRepository : IVaultRepository
    {
        private readonly Supabase.Client _supabase;
        private readonly IConfiguration _configuration;
        private readonly ISessionService _sessionService;
        private static readonly HttpClient SharedHttpClient = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public VaultRepository(Supabase.Client supabase, IConfiguration configuration, ISessionService sessionService)
        {
            _supabase = supabase;
            _configuration = configuration;
            _sessionService = sessionService;
        }

        public async Task CreateUserProfileAsync(UserProfileEntity profile)
        {
            var options = new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal };
            await _supabase.From<UserProfileEntity>().Insert(profile, options);
        }

        public async Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            await WithAuthRequestAsync(async (url, anonKey, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{url}/rest/v1/VaultEntries?Id=eq.{entryId}");
                AddAuthHeaders(request, anonKey, token);
                var response = await SharedHttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            });
        }

        public async Task<List<VaultEntryEntity>> GetAllEntriesAsync(Guid userId)
        {
            return await WithAuthRequestAsync(async (url, anonKey, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{url}/rest/v1/VaultEntries?UserId=eq.{userId}&order=UpdatedAt.desc");
                AddAuthHeaders(request, anonKey, token);
                request.Headers.Add("Accept", "application/json");
                var response = await SharedHttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json)) return new List<VaultEntryEntity>();
                var list = JsonSerializer.Deserialize<List<VaultEntryEntity>>(json, JsonOptions);
                return list ?? new List<VaultEntryEntity>();
            });
        }

        public async Task<VaultEntryEntity?> GetEntryAsync(Guid userId, Guid entryId)
        {
            return await WithAuthRequestAsync(async (url, anonKey, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{url}/rest/v1/VaultEntries?Id=eq.{entryId}&limit=1");
                AddAuthHeaders(request, anonKey, token);
                request.Headers.Add("Accept", "application/json");
                var response = await SharedHttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<VaultEntryEntity>>(json, JsonOptions);
                return list?.FirstOrDefault();
            });
        }

        public async Task<UserProfileEntity?> GetUserProfileAsync(Guid userId)
        {
            return await WithAuthRequestAsync(async (url, anonKey, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{url}/rest/v1/UserProfiles?Id=eq.{userId}&limit=1");
                AddAuthHeaders(request, anonKey, token);
                request.Headers.Add("Accept", "application/json");
                var response = await SharedHttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<UserProfileEntity>>(json, JsonOptions);
                return list?.FirstOrDefault();
            });
        }

        public async Task UpsertEntryAsync(VaultEntryEntity entry)
        {
            entry.UpdatedAt = DateTime.UtcNow;
            await WithAuthRequestAsync(async (url, anonKey, token) =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/VaultEntries");
                AddAuthHeaders(request, anonKey, token);
                request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
                var payload = new
                {
                    Id = entry.Id,
                    UserId = entry.UserId,
                    EncryptedData = entry.EncryptedData,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.UpdatedAt
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), System.Text.Encoding.UTF8, "application/json");
                var response = await SharedHttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            });
        }

        private static void AddAuthHeaders(HttpRequestMessage request, string anonKey, string token)
        {
            request.Headers.Add("apikey", anonKey);
            request.Headers.Add("Authorization", "Bearer " + token);
        }

        private async Task<T> WithAuthRequestAsync<T>(Func<string, string, string, Task<T>> act)
        {
            var url = _configuration["Supabase:Url"]?.TrimEnd('/');
            var anonKey = _configuration["Supabase:AnonKey"];
            var token = _sessionService.GetAccessToken();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(anonKey))
                throw new InvalidOperationException("Supabase:Url and Supabase:AnonKey must be set.");
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Not authenticated. Session access token is missing.");

            return await act(url, anonKey, token);
        }

        private async Task WithAuthRequestAsync(Func<string, string, string, Task> act)
        {
            var url = _configuration["Supabase:Url"]?.TrimEnd('/');
            var anonKey = _configuration["Supabase:AnonKey"];
            var token = _sessionService.GetAccessToken();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(anonKey))
                throw new InvalidOperationException("Supabase:Url and Supabase:AnonKey must be set.");
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Not authenticated. Session access token is missing.");

            await act(url, anonKey, token);
        }
    }
}
