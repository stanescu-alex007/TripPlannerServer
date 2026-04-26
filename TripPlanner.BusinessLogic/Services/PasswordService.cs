using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 4;
        private const int DegreeOfParallelism = 2;
        private const int MemorySize = 1024 * 128;

        // Legacy static salt used before per-password salts were introduced.
        // Kept only for verifying old passwords during migration.
        private static readonly byte[] LegacySalt = Encoding.UTF8.GetBytes("super-secret-salt");

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = ComputeHash(Encoding.UTF8.GetBytes(password), salt);
            return $"v2:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public bool Verify(string password, string storedHash)
        {
            // New format: "v2:<base64 salt>:<base64 hash>"
            if (storedHash.StartsWith("v2:"))
            {
                var parts = storedHash.Split(':', 3);
                if (parts.Length != 3) return false;

                var salt = Convert.FromBase64String(parts[1]);
                var expectedHash = Convert.FromBase64String(parts[2]);
                var actualHash = ComputeHash(Encoding.UTF8.GetBytes(password), salt);
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }

            // Legacy format: plain base64 hash using the static salt
            var legacyHash = ComputeHash(Encoding.UTF8.GetBytes(password), LegacySalt);
            return Convert.ToBase64String(legacyHash) == storedHash;
        }

        private static byte[] ComputeHash(byte[] passwordBytes, byte[] salt)
        {
            var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = DegreeOfParallelism,
                Iterations = Iterations,
                MemorySize = MemorySize
            };
            return argon2.GetBytes(HashSize);
        }
    }
}

