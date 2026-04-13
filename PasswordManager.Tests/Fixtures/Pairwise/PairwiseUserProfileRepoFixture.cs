using PasswordManager.Core.Services.Implementations;
using PasswordManager.Core.Services.Interfaces;
using PasswordManager.Tests.Fakes;

namespace PasswordManager.Tests.Fixtures.Pairwise
{
    public class PairwiseUserProfileRepoFixture
    {
        public IVaultRepository VaultRepository { get; } = new InMemoryVaultRepository();

        public IUserProfileService CreateService() => new UserProfileService(VaultRepository);
    }
}
