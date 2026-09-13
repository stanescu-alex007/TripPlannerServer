using System.Security.Cryptography;
using System.Text;
using OtpNet;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        private const string Issuer = "TripPlanner";

        /// <summary>160 bits — what RFC 4226 recommends for a TOTP shared secret.</summary>
        private const int SecretBytes = 20;

        /// <summary>
        /// One step (30s) of tolerance either way. Enough for a phone whose clock has
        /// drifted; wider would meaningfully enlarge the window for a stolen code.
        /// </summary>
        private const int VerificationWindowSteps = 1;

        private const int RecoveryCodeBytes = 5; // → 8 Base32 characters

        public TwoFactorSetup GenerateSetup(string userEmail)
        {
            var secretBytes = RandomNumberGenerator.GetBytes(SecretBytes);
            var secret = Base32Encoding.ToString(secretBytes).TrimEnd('=');

            // The label carries the account so the user can tell entries apart when
            // they have several in the same authenticator app.
            var label = Uri.EscapeDataString($"{Issuer}:{userEmail}");
            var issuer = Uri.EscapeDataString(Issuer);

            var uri = $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

            return new TwoFactorSetup(secret, uri);
        }

        public bool VerifyCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
                return false;

            var normalized = code.Trim().Replace(" ", string.Empty);
            if (normalized.Length != 6 || !normalized.All(char.IsDigit))
                return false;

            try
            {
                var totp = new Totp(Base32Encoding.ToBytes(PadBase32(secret)));

                return totp.VerifyTotp(
                    normalized,
                    out _,
                    new VerificationWindow(
                        previous: VerificationWindowSteps,
                        future: VerificationWindowSteps));
            }
            catch (ArgumentException)
            {
                // Malformed secret — treat as a failed check rather than a crash.
                return false;
            }
        }

        public IReadOnlyList<string> GenerateRecoveryCodes(int count = 10)
        {
            var codes = new List<string>(count);

            for (var i = 0; i < count; i++)
            {
                var raw = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(RecoveryCodeBytes))
                                        .TrimEnd('=');

                // Hyphenated so it is readable and transcribable off a printed page.
                codes.Add($"{raw[..4]}-{raw[4..]}".ToLowerInvariant());
            }

            return codes;
        }

        /// <summary>Base32Encoding.ToBytes needs the padding that ToString trimmed off.</summary>
        private static string PadBase32(string value)
        {
            var remainder = value.Length % 8;
            return remainder == 0 ? value : value + new string('=', 8 - remainder);
        }
    }
}
