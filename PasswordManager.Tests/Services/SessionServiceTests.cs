using PasswordManager.Core.Services.Implementations;

namespace PasswordManager.Tests.Services
{
    public class SessionServiceTests : IDisposable
    {
        private readonly SessionService _sessionService;

        public SessionServiceTests()
        {
            _sessionService = new SessionService();
        }

        public void Dispose()
        {
            _sessionService.Dispose();
        }

        [Fact]
        public void SetDerivedKeyThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _sessionService.SetDerivedKey(new byte[32]));
        }

        [Fact]
        public void SettingValidDerivedKeyActivatesSession()
        {
            var key = new byte[32];

            _sessionService.SetDerivedKey(key);

            Assert.True(_sessionService.IsActive());
        }

        [Fact]
        public void SettingNullDerivedKeyThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => _sessionService.SetDerivedKey(null!));
        }

        [Fact]
        public void SettingDerivedKeyTwiceWipesOldKey()
        {
            var oldKey = new byte[32];
            var newKey = new byte[32];

            _sessionService.SetDerivedKey(oldKey);

            _sessionService.SetDerivedKey(newKey);

            Assert.All(oldKey, b => Assert.Equal(0, b));
        }

        [Fact]
        public void SettingDerivedKeyTwiceKeepsSessionAlive()
        {
            var oldKey = new byte[32];
            var newKey = new byte[32];

            _sessionService.SetDerivedKey(oldKey);

            _sessionService.SetDerivedKey(newKey);

            Assert.True(_sessionService.IsActive());
        }

        [Fact]
        public void SettingDerivedKeyResetsInactivityTimer()
        {
            _sessionService.InactivityTimeout = TimeSpan.FromMilliseconds(200);
            Thread.Sleep(100);
            _sessionService.SetDerivedKey(new byte[32]);

            Thread.Sleep(150);
            Assert.True(_sessionService.IsActive());
        }

        [Fact]
        public void GetDerivedKeyThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _sessionService.GetDerivedKey());
        }

        [Fact]
        public void GetDerivedKeyReturnsSetKeyWhenSessionIsActive()
        {
            var key = new byte[32];
            _sessionService.SetDerivedKey(key);
            var retrievedKey = _sessionService.GetDerivedKey();
            Assert.Equal(key, retrievedKey);
        }

        [Fact]
        public void GetDerivedKeyReturnsCloneOfSetKey()
        {
            var key = new byte[32];
            Array.Fill(key, (byte)0xAB);
            _sessionService.SetDerivedKey(key);

            var retrievedKey = _sessionService.GetDerivedKey();
            retrievedKey[0] = 0xFF; // mutate the returned clone in-place

            var retrievedKey2 = _sessionService.GetDerivedKey();

            Assert.Equal(key, retrievedKey2);
        }

        [Fact]
        public void GetDerivedKeyThrowsWhenSessionIsInactive()
        {
            Assert.Throws<InvalidOperationException>(() => _sessionService.GetDerivedKey());
        }

        [Fact]
        public void GetDerivedKeyThrowsWhenSessionIsCleared()
        {
            _sessionService.SetDerivedKey(new byte[32]);
            _sessionService.ClearSession();

            Assert.Throws<InvalidOperationException>(() => _sessionService.GetDerivedKey());
        }


        [Fact]
        public void SetUserThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _sessionService.SetUser(Guid.NewGuid(), "email@dot.com"));
        }

        public static IEnumerable<object[]> ValidUserData()
        {
            yield return new object[] { Guid.NewGuid(), "user@user.user", "token" };
            yield return new object[] { Guid.NewGuid(), "user@user.user", null };
        }

        [Theory]
        [MemberData(nameof(ValidUserData))]
        public void SetUserCanBeUsedDuringInactiveSession(Guid userId, string userEmail, string? userToken)
        {
            _sessionService.SetUser(userId, userEmail, userToken);

            Assert.Equal(userId, _sessionService.CurrentUserId);
            Assert.Equal(userEmail, _sessionService.CurrentUserEmail);
            Assert.Equal(userToken, _sessionService.GetAccessToken());
        }

        [Theory]
        [MemberData(nameof(ValidUserData))]
        public void SetUserCanBeUsedDuringActiveSession(Guid userId, string userEmail, string? userToken)
        {
            _sessionService.SetDerivedKey(new byte[32]);
            _sessionService.SetUser(userId, userEmail, userToken);

            Assert.Equal(userId, _sessionService.CurrentUserId);
            Assert.Equal(userEmail, _sessionService.CurrentUserEmail);
            Assert.Equal(userToken, _sessionService.GetAccessToken());
        }

        [Fact]
        public void SetUserThrowsWhenEmailIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _sessionService.SetUser(Guid.NewGuid(), null!));
        }

        [Fact]
        public void GetAccessTokenThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _sessionService.GetAccessToken());
        }

        [Fact]
        public void GetAccessTokenReturnsNullWhenNoTokenSet()
        {
            Assert.Null(_sessionService.GetAccessToken());
        }

        [Fact]
        public void GetAccessTokenReturnsSetToken()
        {
            var token = "token";

            _sessionService.SetUser(Guid.NewGuid(), "email@email.email", token);

            Assert.Equal(token, _sessionService.GetAccessToken());
        }

        [Fact]
        public void CurrentUserIdReturnsNullWhenNoUserSet()
        {
            Assert.Null(_sessionService.CurrentUserId);
        }

        [Fact]
        public void CurrentUserEmailReturnsNullWhenNoUserSet()
        {
            Assert.Null(_sessionService.CurrentUserEmail);
        }

        [Fact]
        public void CurrentUserIdReturnsSetUserId()
        {
            var userId = Guid.NewGuid();
            _sessionService.SetUser(userId, "a@a.x");

            Assert.Equal(userId, _sessionService.CurrentUserId);
        }

        [Fact]
        public void CurrentUserEmailReturnsSetUserEmail()
        {
            var userEmail = "a@a.x";
            _sessionService.SetUser(Guid.NewGuid(), userEmail);
            Assert.Equal(userEmail, _sessionService.CurrentUserEmail);
        }

        [Fact]
        public void ClearSessionWipesDerivedKeyAndUserData()
        {
            var key = new byte[32];
            var userId = Guid.NewGuid();
            var userEmail = "a@a.x";
            var token = "token";

            _sessionService.SetDerivedKey(key);
            _sessionService.SetUser(userId, userEmail, token);
            _sessionService.ClearSession();

            Assert.Throws<InvalidOperationException>(() => _sessionService.GetDerivedKey());
            Assert.Null(_sessionService.CurrentUserId);
            Assert.Null(_sessionService.CurrentUserEmail);
            Assert.Null(_sessionService.GetAccessToken());
        }

        [Fact]
        public void ClearSessionDeactivatesSession()
        {
            _sessionService.SetDerivedKey(new byte[32]);
            _sessionService.ClearSession();
            Assert.False(_sessionService.IsActive());
        }

        [Fact]
        public void ClearSessionRaisesVaultLockedEvent()
        {
            var eventRaised = false;
            _sessionService.VaultLocked += (s, e) => eventRaised = true;
            _sessionService.ClearSession();
            Assert.True(eventRaised);
        }

        [Fact]
        public void ClearSessionOnDisposedSessionDoesNotThrow()
        {
            _sessionService.Dispose();

            var exception = Record.Exception(() => _sessionService.ClearSession());

            Assert.Null(exception);
        }

        [Fact]
        public void SessionIsnNotActiveOnCreation()
        {
            Assert.False(_sessionService.IsActive());
        }

        [Fact]
        public void IsActiveThrowsWhenDisposed()
        {
            _sessionService.SetDerivedKey(new byte[32]);
            _sessionService.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _sessionService.IsActive());
        }

        [Fact]
        public void ResetInactivityTimerResetsTimeout()
        {
            _sessionService.InactivityTimeout = TimeSpan.FromMilliseconds(200);
            _sessionService.SetDerivedKey(new byte[32]);

            Thread.Sleep(100);

            _sessionService.ResetInactivityTimer();
            Thread.Sleep(150);

            Assert.True(_sessionService.IsActive());
        }

        [Fact]
        public void ResetInactivityTimerThrowsWhenSessionDisposed()
        {
            _sessionService.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _sessionService.ResetInactivityTimer());
        }

        [Fact]
        public void InactivityTimeoutThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _sessionService.InactivityTimeout = TimeSpan.FromMinutes(1));
        }

        [Fact]
        public void InactivityTimeoutGetterThrowsWhenSessionIsDisposed()
        {
            _sessionService.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = _sessionService.InactivityTimeout);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1000)]
        [InlineData(300000)]
        public void InactivityTimeoutAcceptsPositiveValues(int milliseconds)
        {
            var timeout = TimeSpan.FromMilliseconds(milliseconds);

            _sessionService.InactivityTimeout = timeout;

            Assert.Equal(timeout, _sessionService.InactivityTimeout);
        }

        [Fact]
        public void InactivityTimeoutThrowsForZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _sessionService.InactivityTimeout = TimeSpan.Zero);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-1000)]
        public void InactivityTimeoutThrowsForNegativeValues(int milliseconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _sessionService.InactivityTimeout = TimeSpan.FromMilliseconds(milliseconds));
        }

    }
}
