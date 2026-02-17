using PasswordManager.Core.Entities;
using PasswordManager.Core.Models;
using System.Threading.Tasks;

namespace PasswordManager.Core.Services.Interfaces
{
    /// <summary>
    /// Inserts a user profile via a single authenticated request (e.g. direct HTTP with JWT).
    /// Used when the Supabase client does not send the user token on the next request (e.g. after SignUp).
    /// </summary>
    public interface IUserProfileInserterWithToken
    {
        Task<Result> InsertAsync(UserProfileEntity profile, string accessToken);
    }
}
