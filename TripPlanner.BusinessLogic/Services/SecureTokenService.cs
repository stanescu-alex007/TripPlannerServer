using System.Security.Cryptography;
using System.Text;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    public class SecureTokenService : ISecureTokenService
    {
        /// <summary>256 bits — the same strength as the hash that stores it.</summary>
        private const int TokenBytes = 32;

        public string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(TokenBytes);

            // Base64Url: no '+', '/' or '=' to escape when the token travels in a URL.
            return Base64UrlEncode(bytes);
        }

        public string Hash(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                   .TrimEnd('=')
                   .Replace('+', '-')
                   .Replace('/', '_');
    }
}
