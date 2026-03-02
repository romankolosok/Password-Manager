using Bogus;

namespace PasswordManager.Tests
{
    /// <summary>
    /// Central source of realistic fake values for all test projects.
    /// The seed is fixed so every test run produces the same values,
    /// making failures deterministic and reproducible in CI.
    /// </summary>
    public static class TestData
    {
        private const int Seed = 121;

        // One Faker per domain — each is seeded so output is deterministic.
        private static readonly Faker _faker = new Faker { Random = new Randomizer(Seed) };

        /// <summary>A random email address</summary>
        public static string Email() => _faker.Internet.Email();

        /// <summary>A random username"</summary>
        public static string Username() => _faker.Internet.UserName();

        /// <summary>A random password</summary>
        public static string Password() => _faker.Internet.Password(length: 16, memorable: false, prefix: "A1!");

        /// <summary>A random JWT-shaped access token string.</summary>
        public static string AccessToken() => _faker.Random.AlphaNumeric(128);

        /// <summary>A realistic website name</summary>
        public static string WebsiteName() => _faker.Company.CompanyName();

        /// <summary>A realistic URL"</summary>
        public static string Url() => _faker.Internet.Url();

        /// <summary>A short sentence suitable for a vault entry notes field.</summary>
        public static string Notes() => _faker.Lorem.Sentence();

        /// <summary>A vault entry category word, e.g. "Finance"</summary>
        public static string Category() => _faker.Commerce.Department();


        /// <summary>A random 32-byte derived key.</summary>
        public static byte[] DerivedKey()
        {
            var key = new byte[32];
            _faker.Random.Bytes(32).CopyTo(key, 0);
            return key;
        }

        /// <summary>A random user id.</summary>
        public static Guid UserId() => _faker.Random.Guid();
    }
}