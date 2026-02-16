using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using System;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    public interface IUserProfileService
    {
        Task<Result> CreateProfileAsync(UserProfileEntity profile, string accessToken);
        Task<Result<UserProfileEntity>> GetProfileAsync(Guid userId);
    }
}