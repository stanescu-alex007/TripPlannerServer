namespace TripPlanner.Core.Interfaces.IServices
{
    public sealed record TwoFactorSetup(
        /// <summary>Base32 secret, shown so it can be typed in manually.</summary>
        string Secret,

        /// <summary>
        /// otpauth:// URI the client renders as a QR code. Deliberately built here
        /// rather than rendering an image server-side — the raw URI is smaller, and
        /// the client can offer both the QR and the typed secret from one value.
        /// </summary>
        string OtpAuthUri);

    public interface ITwoFactorService
    {
        /// <summary>Creates a new secret and its provisioning URI. Nothing is stored yet.</summary>
        TwoFactorSetup GenerateSetup(string userEmail);

        /// <summary>
        /// Checks a six-digit code against the secret, allowing one step either side so
        /// a slightly out-of-sync device still works.
        /// </summary>
        /// <param name="timeStep">
        /// Which 30-second step the code belongs to. Recorded by the caller so the same
        /// code cannot be presented twice while its window is still open.
        /// </param>
        bool VerifyCode(string secret, string code, out long timeStep);

        /// <summary>Fresh recovery codes, in the plain form shown to the user once.</summary>
        IReadOnlyList<string> GenerateRecoveryCodes(int count = 10);
    }
}
