using PasswordManager.Core.Services.Interfaces;
using System.Threading;

namespace PasswordManager.App.Services
{
    internal sealed class PostgrestAuthTokenOverride : IPostgrestAuthTokenOverride
    {
        private string? _token;

        public void SetTokenForNextRequest(string? accessToken) => _token = accessToken;

        public string? GetAndClearToken() => Interlocked.Exchange(ref _token, null);
    }
}
