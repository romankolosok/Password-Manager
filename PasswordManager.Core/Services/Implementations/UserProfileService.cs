using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using PasswordManager.Core.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Implementations
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IVaultRepository _vaultRepository;

        public UserProfileService(IVaultRepository vaultRepository)
        {
            _vaultRepository = vaultRepository;
        }

        public async Task<Result> CreateProfileAsync(UserProfileEntity profile)
        {
            try
            {
                await _vaultRepository.CreateUserProfileAsync(profile);
                return Result.Ok();
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException ex)
            {
                // Supabase-specific exceptions (e.g., constraint violations, RLS policy failures)
                return Result.Fail($"Database error while creating profile: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result.Fail($"Failed to create user profile: {ex.Message}");
            }
        }

        public async Task<Result<UserProfileEntity>> GetProfileAsync(Guid userId)
        {
            try
            {
                var profile = await _vaultRepository.GetUserProfileAsync(userId);

                if (profile == null)
                {
                    return Result<UserProfileEntity>.Fail("User profile not found.");
                }

                return Result<UserProfileEntity>.Ok(profile);
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException ex)
            {
                return Result<UserProfileEntity>.Fail($"Database error while fetching profile: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result<UserProfileEntity>.Fail($"Failed to get user profile: {ex.Message}");
            }
        }
    }
}