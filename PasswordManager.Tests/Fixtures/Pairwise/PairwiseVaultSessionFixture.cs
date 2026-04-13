using Microsoft.Extensions.Logging;
using Moq;
using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseVaultSessionFixture : IDisposable
    {
        private ISessionService _session = new SessionService();

        public ISessionService SessionService => _session;

        public Mock<ICryptoService> CryptoService { get; } = new();
        public Mock<IVaultRepository> VaultRepository { get; } = new();
        public Mock<ILogger<VaultService>> Logger { get; } = new();

        public VaultService CreateService() =>
            new(CryptoService.Object, _session, VaultRepository.Object, Logger.Object);

        public void Reset()
        {
            CryptoService.Reset();
            VaultRepository.Reset();
            Logger.Reset();
            _session.Dispose();
            _session = new SessionService();
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
