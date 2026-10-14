using System.Security.Cryptography;
using System.Text;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    /// <summary>
    /// AES-256-GCM over a configured key ring.
    ///
    /// <para>
    /// Stored form: <c>v1.{keyId}.{base64(nonce ‖ tag ‖ ciphertext)}</c>. The key id
    /// travels with the value, so rotating means adding a key and pointing
    /// <c>ActiveKeyId</c> at it — existing values keep decrypting with the key that wrote
    /// them. A key can be removed only once no stored value references it.
    /// </para>
    ///
    /// <para>
    /// Chosen over ASP.NET Core Data Protection, whose documentation states it is not
    /// intended for indefinitely persisted payloads: its keys roll and expire on their
    /// own schedule, and a lost key ring silently turns every stored secret into noise.
    /// These keys are plain configuration, kept and backed up like the JWT signing key.
    /// </para>
    /// </summary>
    public sealed class AesGcmSecretProtector : ISecretProtector
    {
        private const string Version = "v1";
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly SecretProtectionKeys _keys;

        public AesGcmSecretProtector(SecretProtectionKeys keys) => _keys = keys;

        public string Marker => Version + ".";

        public string Protect(string plaintext, string purpose)
        {
            var plain = Encoding.UTF8.GetBytes(plaintext);
            var payload = new byte[NonceSize + TagSize + plain.Length];

            var nonce = payload.AsSpan(0, NonceSize);
            RandomNumberGenerator.Fill(nonce);

            using (var aes = new AesGcm(_keys.ActiveKey, TagSize))
            {
                aes.Encrypt(
                    nonce,
                    plain,
                    payload.AsSpan(NonceSize + TagSize),
                    payload.AsSpan(NonceSize, TagSize),
                    Encoding.UTF8.GetBytes(purpose));
            }

            return $"{Version}.{_keys.ActiveKeyId}.{Convert.ToBase64String(payload)}";
        }

        public string? Unprotect(string protectedValue, string purpose)
        {
            // Base64 never contains '.', so exactly three parts or it is not ours.
            var parts = protectedValue.Split('.');
            if (parts.Length != 3 || parts[0] != Version) return null;

            if (!_keys.TryGetKey(parts[1], out var key)) return null;

            var payload = new byte[(parts[2].Length * 3 + 3) / 4];
            if (!Convert.TryFromBase64String(parts[2], payload, out var length)) return null;
            if (length < NonceSize + TagSize) return null;

            var plain = new byte[length - NonceSize - TagSize];

            try
            {
                using var aes = new AesGcm(key, TagSize);

                aes.Decrypt(
                    payload.AsSpan(0, NonceSize),
                    payload.AsSpan(NonceSize + TagSize, plain.Length),
                    payload.AsSpan(NonceSize, TagSize),
                    plain,
                    Encoding.UTF8.GetBytes(purpose));
            }
            catch (CryptographicException)
            {
                // Tampered, truncated, or bound to a different purpose.
                return null;
            }

            return Encoding.UTF8.GetString(plain);
        }
    }

    /// <summary>The configured keys, validated once at startup.</summary>
    public sealed class SecretProtectionKeys
    {
        private readonly IReadOnlyDictionary<string, byte[]> _keys;

        public SecretProtectionKeys(string activeKeyId, IReadOnlyDictionary<string, byte[]> keys)
        {
            if (!keys.ContainsKey(activeKeyId))
                throw new ArgumentException($"Active key '{activeKeyId}' is not in the key ring.", nameof(activeKeyId));

            ActiveKeyId = activeKeyId;
            _keys = keys;
        }

        public string ActiveKeyId { get; }

        public byte[] ActiveKey => _keys[ActiveKeyId];

        public bool TryGetKey(string keyId, out byte[] key) => _keys.TryGetValue(keyId, out key!);
    }
}
