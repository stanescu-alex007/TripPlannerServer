using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    /// <summary>
    /// Argon2id with a per-password salt. Stored format: <c>v2:{base64 salt}:{base64 hash}</c>.
    /// </summary>
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 3;
        private const int DegreeOfParallelism = 2;

        /// <summary>
        /// 64 MB. Deliberately lower than the previous 128 MB: every login allocates this,
        /// so with concurrent requests the old setting was an easy way to exhaust memory.
        /// </summary>
        private const int MemorySizeKb = 64 * 1024;

        /// <summary>
        /// Hashed against when the account does not exist, purely to burn the same amount
        /// of time as a real verification.
        /// </summary>
        private static readonly Lazy<string> DummyHash =
            new(() => new PasswordService().Hash("dummy-password-for-timing-equalisation"));

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = ComputeHash(Encoding.UTF8.GetBytes(password), salt);
            return $"v2:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split(':', 3);
            if (parts.Length != 3 || parts[0] != "v2") return false;

            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = ComputeHash(Encoding.UTF8.GetBytes(password), salt);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public bool VerifyDummy(string password)
        {
            Verify(password, DummyHash.Value);
            return false;
        }

        private static byte[] ComputeHash(byte[] password, byte[] salt)
        {
            using var argon2 = new Argon2id(password)
            {
                Salt = salt,
                DegreeOfParallelism = DegreeOfParallelism,
                Iterations = Iterations,
                MemorySize = MemorySizeKb
            };

            return argon2.GetBytes(HashSize);
        }
    }
}
